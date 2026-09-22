using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameAct.Les.Shared;
using GameAct.Les.Transport;
using GameAct.Les.View;
using GameAct.Net;

namespace GameAct.Les
{
    /// <summary>
    /// LES 传输中枢：支持 LiteNet UDP 与 Steam Networking Sockets P2P。
    /// 应用层（EntityManager / ActPlayer）与传输无关；由 NetTransportKind 选择后端。
    /// Solo 不走本类（仍用 LesAuthoritySession 离线权威）。
    /// </summary>
    public sealed class LesNetworkHub : INetSession, INetEventListener, IDisposable
    {
        public const int DefaultPort = 9050;
        public const int DefaultMonsterCount = 12;
        public const int DefaultSteamVirtualPort = SteamP2PTransport.DefaultVirtualPort;
        const string ConnectKey = "game-act-les";

        public NetRole Role { get; private set; } = NetRole.None;
        public bool IsConnected { get; private set; }
        public int PeerCount
        {
            get
            {
                if (_activeTransport == NetTransportKind.SteamP2P)
                    return _steam?.ConnectionCount ?? 0;
                return _manager?.ConnectedPeersCount ?? 0;
            }
        }
        public string StatusText { get; private set; } = "Idle";
        public NetTransportKind ActiveTransport => _activeTransport;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnLog;

        public ServerEntityManager ServerEm { get; private set; }
        public ClientEntityManager ClientEm { get; private set; }
        public ulong TypesHash { get; private set; }

        // ─── UDP (LiteNet) ──────────────────────────────────
        NetManager _manager;
        NetPeer _serverPeer;
        NetPacketProcessor _packetProcessor;
        readonly NetDataWriter _writer = new NetDataWriter();

        // ─── Steam P2P ──────────────────────────────────────
        SteamP2PTransport _steam;
        SteamP2PNetPeer _steamServerPeer; // Client 侧指向 Host 的 peer

        NetTransportKind _activeTransport = NetTransportKind.Udp;

        readonly List<MonsterView> _views = new List<MonsterView>(32);
        Transform _viewRoot;
        string _levelScene;
        string _userName = "Player";
        bool _enemiesSpawned;

        // 可选延迟模拟（仅 UDP 生效；本机测预测用，默认关）
        public bool SimulateLatency { get; set; }
        public int SimulationMinLatencyMs { get; set; } = 50;
        public int SimulationMaxLatencyMs { get; set; } = 80;
        public bool SimulatePacketLoss { get; set; }
        public int SimulationPacketLossChance { get; set; } = 5;

        public void SetUserName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _userName = name.Trim();
        }

        public async UniTask<bool> StartHostAsync(int port = DefaultPort, NetTransportKind transport = NetTransportKind.Udp)
        {
            Disconnect();
            _activeTransport = transport;

            var typesMap = LesTypesMapFactory.Create();
            TypesHash = typesMap.EvaluateEntityClassDataHash();
            ServerEm = new ServerEntityManager(
                typesMap,
                LesTypesMapFactory.HeaderByte,
                (byte)LesTypesMapFactory.TickRate,
                ServerSendRate.EqualToFPS);
            ClientEm = null;

            if (transport == NetTransportKind.SteamP2P)
                return await StartHostSteamAsync();

            // 默认 / Udp / InProcess → LiteNet UDP
            return await StartHostUdpAsync(port);
        }

        public async UniTask<bool> ConnectAsync(string address, int port = DefaultPort, NetTransportKind transport = NetTransportKind.Udp)
        {
            Disconnect();
            _activeTransport = transport;

            if (transport == NetTransportKind.SteamP2P)
            {
                // address 约定为 Host 的 SteamID64 字符串
                if (!ulong.TryParse(address, out var steamId) || steamId == 0)
                {
                    StatusText = "SteamP2P 需要 Host SteamID64 作为 address";
                    Log(StatusText);
                    return false;
                }
                return await ConnectSteamAsync(steamId, port > 0 ? port : DefaultSteamVirtualPort);
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                StatusText = "地址为空";
                return false;
            }

            return await ConnectUdpAsync(address, port);
        }

        public void Disconnect()
        {
            ClearViews();

            if (_steam != null)
            {
                _steam.OnPeerConnected -= OnSteamPeerConnected;
                _steam.OnPeerDisconnected -= OnSteamPeerDisconnected;
                _steam.OnDataReceived -= OnSteamDataReceived;
                _steam.OnLog -= OnSteamLog;
                _steam.Dispose();
                _steam = null;
            }
            _steamServerPeer = null;

            if (_manager != null)
            {
                _manager.Stop();
                _manager = null;
            }
            _serverPeer = null;
            ServerEm = null;
            ClientEm = null;
            _packetProcessor = null;
            _enemiesSpawned = false;
            _activeTransport = NetTransportKind.Udp;

            var was = IsConnected || Role != NetRole.None;
            IsConnected = false;
            Role = NetRole.None;
            StatusText = "Disconnected";
            if (was) OnDisconnected?.Invoke();
        }

        public void SendReliable(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            if (_activeTransport == NetTransportKind.SteamP2P && _steam != null)
            {
                if (Role == NetRole.Client && _steamServerPeer != null)
                    _steamServerPeer.SendReliableOrdered(data);
                // Host 广播：LES 自行按 peer 发送，此处仅占位
                return;
            }

            if (_manager == null) return;
            var w = new NetDataWriter();
            w.Put(data);
            if (Role == NetRole.Host)
                _manager.SendToAll(w, DeliveryMethod.ReliableOrdered);
            else
                _serverPeer?.Send(w, DeliveryMethod.ReliableOrdered);
        }

        public void Poll()
        {
            if (_activeTransport == NetTransportKind.SteamP2P)
                _steam?.Poll();
            else
                _manager?.PollEvents();

            ServerEm?.Update();
            ClientEm?.Update();
            SyncMonsterViews();
        }

        public void Dispose() => Disconnect();

        /// <summary>Host/Solo 权威刷怪；Client 靠快照构造。</summary>
        public void SpawnMonstersAround(Vector3 center, string levelScene, int count = DefaultMonsterCount)
        {
            if (ServerEm == null || _enemiesSpawned) return;
            _levelScene = levelScene;
            EnsureViewRoot(levelScene);

            int n = Mathf.Clamp(count, 0, 64);
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                float radius = 6f + (i % 3) * 2.5f;
                var pos = SnapToGround(center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));

                var monster = ServerEm.AddEntity<ActMonster>(e => e.Spawn(pos));
                ServerEm.AddAIController<MonsterBotController>(c => c.StartControl(monster));

                var view = MonsterView.Create(monster.Id, pos, _viewRoot);
                MoveToLevel(view.gameObject, levelScene);
                _views.Add(view);
            }

            _enemiesSpawned = true;
            Log($"Spawned {n} LES enemies around {center}");
        }

        /// <summary>
        /// Host/权威本地玩家：无 HumanController，DriveLocally 读输入。
        /// </summary>
        public ActPlayer SpawnLocalPlayer(Vector3 position)
        {
            if (ServerEm == null)
                throw new InvalidOperationException("ServerEm is null");
            var player = ServerEm.AddEntity<ActPlayer>(e =>
            {
                e.Spawn(position);
                e.SetDriveLocally(true);
            });
            Log($"Spawned local ActPlayer id={player.Id}");
            return player;
        }

        // ═══════════════════════════════════════════════════
        // UDP backend
        // ═══════════════════════════════════════════════════

        async UniTask<bool> StartHostUdpAsync(int port)
        {
            _packetProcessor = new NetPacketProcessor();
            _packetProcessor.SubscribeReusable<JoinPacket, NetPeer>(OnJoinReceivedUdp);

            _manager = new NetManager(this)
            {
                AutoRecycle = true,
                DisconnectTimeout = 10000
            };
            ApplySimulationSettings(_manager);

            if (!_manager.Start(port))
            {
                StatusText = "LES Host 启动失败 port=" + port;
                Log(StatusText);
                ServerEm = null;
                return false;
            }

            Role = NetRole.Host;
            IsConnected = true;
            StatusText = $"LES Host UDP :{port} hash={TypesHash:X}";
            Log(StatusText);
            OnConnected?.Invoke();
            await UniTask.Yield();
            return true;
        }

        async UniTask<bool> ConnectUdpAsync(string address, int port)
        {
            _packetProcessor = new NetPacketProcessor();
            _manager = new NetManager(this)
            {
                AutoRecycle = true,
                DisconnectTimeout = 10000
            };
            ApplySimulationSettings(_manager);

            if (!_manager.Start())
            {
                StatusText = "LES Client NetManager 启动失败";
                Log(StatusText);
                return false;
            }

            Role = NetRole.Client;
            StatusText = $"Connecting UDP {address}:{port}…";
            Log(StatusText);
            _manager.Connect(address, port, ConnectKey);

            var t0 = Time.realtimeSinceStartup;
            while (!IsConnected && Time.realtimeSinceStartup - t0 < 8f)
            {
                _manager.PollEvents();
                await UniTask.Yield();
            }

            if (!IsConnected)
            {
                StatusText = "连接超时";
                Log(StatusText);
                Disconnect();
                return false;
            }

            return true;
        }

        void ApplySimulationSettings(NetManager m)
        {
            m.SimulateLatency = SimulateLatency;
            m.SimulationMinLatency = SimulationMinLatencyMs;
            m.SimulationMaxLatency = SimulationMaxLatencyMs;
            m.SimulatePacketLoss = SimulatePacketLoss;
            m.SimulationPacketLossChance = SimulationPacketLossChance;
        }

        void OnJoinReceivedUdp(JoinPacket join, NetPeer peer)
        {
            Log($"Join(UDP) from {peer} user={join.UserName} hash={join.GameHash:X}");
            if (ServerEm == null)
            {
                peer.Disconnect();
                return;
            }
            if (join.GameHash != TypesHash)
            {
                Log("Client types hash mismatch → disconnect");
                peer.Disconnect();
                return;
            }

            var abstractPeer = peer.Tag as AbstractNetPeer ?? new GameActNetPeer(peer, true);
            if (peer.Tag == null)
                peer.Tag = abstractPeer;

            SpawnRemotePlayer(abstractPeer, join.UserName);
        }

        // ═══════════════════════════════════════════════════
        // Steam P2P backend
        // ═══════════════════════════════════════════════════

        async UniTask<bool> StartHostSteamAsync()
        {
            _steam = new SteamP2PTransport();
            _steam.OnPeerConnected += OnSteamPeerConnected;
            _steam.OnPeerDisconnected += OnSteamPeerDisconnected;
            _steam.OnDataReceived += OnSteamDataReceived;
            _steam.OnLog += OnSteamLog;

            if (!_steam.StartHost(DefaultSteamVirtualPort))
            {
                StatusText = _steam.StatusText;
                Log(StatusText);
                ServerEm = null;
                _steam.Dispose();
                _steam = null;
                return false;
            }

            Role = NetRole.Host;
            IsConnected = true;
            StatusText = $"LES Host SteamP2P hash={TypesHash:X}";
            Log(StatusText);
            OnConnected?.Invoke();
            await UniTask.Yield();
            return true;
        }

        async UniTask<bool> ConnectSteamAsync(ulong hostSteamId, int virtualPort)
        {
            _steam = new SteamP2PTransport();
            _steam.OnPeerConnected += OnSteamPeerConnected;
            _steam.OnPeerDisconnected += OnSteamPeerDisconnected;
            _steam.OnDataReceived += OnSteamDataReceived;
            _steam.OnLog += OnSteamLog;

            if (!_steam.Connect(hostSteamId, virtualPort))
            {
                StatusText = _steam.StatusText;
                Log(StatusText);
                _steam.Dispose();
                _steam = null;
                return false;
            }

            Role = NetRole.Client;
            StatusText = $"Connecting SteamP2P → {hostSteamId}…";
            Log(StatusText);

            var t0 = Time.realtimeSinceStartup;
            while (!IsConnected && Time.realtimeSinceStartup - t0 < 12f)
            {
                // Steam 状态回调依赖 SteamAPI.RunCallbacks（SteamRunner）
                _steam.Poll();
                await UniTask.Yield();
            }

            if (!IsConnected)
            {
                StatusText = "SteamP2P 连接超时";
                Log(StatusText);
                Disconnect();
                return false;
            }

            return true;
        }

        void OnSteamPeerConnected(SteamP2PNetPeer peer)
        {
            Log($"Steam peer connected {peer}");

            if (Role == NetRole.Client)
            {
                _steamServerPeer = peer;
                var typesMap = LesTypesMapFactory.Create();
                TypesHash = typesMap.EvaluateEntityClassDataHash();

                // 发 Join（与 UDP 相同包格式）
                SendSteamJoin(peer);

                ClientEm = new ClientEntityManager(
                    typesMap,
                    peer,
                    LesTypesMapFactory.HeaderByte);
                ServerEm = null;

                IsConnected = true;
                StatusText = $"LES Client SteamP2P → {peer.RemoteSteamId.m_SteamID}";
                Log(StatusText);
                OnConnected?.Invoke();
            }
            // Host：等 Join 包再 AddPlayer
        }

        void OnSteamPeerDisconnected(SteamP2PNetPeer peer, string reason)
        {
            Log($"Steam peer disconnected {peer} reason={reason}");
            if (Role == NetRole.Client)
            {
                IsConnected = false;
                StatusText = "Disconnected: " + reason;
                ClientEm = null;
                _steamServerPeer = null;
                Role = NetRole.None;
                OnDisconnected?.Invoke();
            }
            else if (Role == NetRole.Host && ServerEm != null)
            {
                // LES 会在下次 Update 感知 peer 失效；此处仅日志
                StatusText = $"LES Host SteamP2P peers={PeerCount}";
            }
        }

        void OnSteamDataReceived(SteamP2PNetPeer peer, byte[] data)
        {
            if (data == null || data.Length < 1) return;
            var packetType = (LesPacketType)data[0];

            switch (packetType)
            {
                case LesPacketType.EntitySystem:
                {
                    // 整包交给 LES（含 type 字节，与 UDP ToSpan 行为一致）
                    var span = new ReadOnlySpan<byte>(data);
                    if (Role == NetRole.Host && ServerEm != null)
                        ServerEm.Deserialize(peer, span);
                    else if (Role == NetRole.Client && ClientEm != null)
                        ClientEm.Deserialize(span);
                    break;
                }
                case LesPacketType.Serialized:
                {
                    // 简易 Join 解析（不依赖 LiteNet PacketProcessor）
                    if (Role == NetRole.Host)
                        TryHandleSteamJoin(peer, data);
                    break;
                }
                default:
                    Log("Unhandled Steam packet " + packetType);
                    break;
            }
        }

        void OnSteamLog(string msg) => OnLog?.Invoke(msg);

        void SendSteamJoin(SteamP2PNetPeer peer)
        {
            // 布局： [LesPacketType.Serialized=1][userLen:u16][user utf8][gameHash:u64]
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(_userName ?? "Player");
            if (nameBytes.Length > 64)
                Array.Resize(ref nameBytes, 64);

            var buf = new byte[1 + 2 + nameBytes.Length + 8];
            int o = 0;
            buf[o++] = (byte)LesPacketType.Serialized;
            buf[o++] = (byte)(nameBytes.Length & 0xff);
            buf[o++] = (byte)((nameBytes.Length >> 8) & 0xff);
            Buffer.BlockCopy(nameBytes, 0, buf, o, nameBytes.Length);
            o += nameBytes.Length;
            var hashBytes = BitConverter.GetBytes(TypesHash);
            Buffer.BlockCopy(hashBytes, 0, buf, o, 8);

            peer.SendReliableOrdered(buf);
            Log($"Sent Steam Join user={_userName} hash={TypesHash:X}");
        }

        void TryHandleSteamJoin(SteamP2PNetPeer peer, byte[] data)
        {
            // data[0]=Serialized；后续为自定义布局
            if (data.Length < 1 + 2 + 8) return;
            int o = 1;
            int nameLen = data[o] | (data[o + 1] << 8);
            o += 2;
            if (nameLen < 0 || nameLen > 64 || o + nameLen + 8 > data.Length) return;

            string userName = System.Text.Encoding.UTF8.GetString(data, o, nameLen);
            o += nameLen;
            ulong gameHash = BitConverter.ToUInt64(data, o);

            Log($"Join(Steam) from {peer.RemoteSteamId.m_SteamID} user={userName} hash={gameHash:X}");

            if (ServerEm == null)
            {
                _steam.DisconnectPeer(peer.Connection);
                return;
            }
            if (gameHash != TypesHash)
            {
                Log("Client types hash mismatch → disconnect");
                _steam.DisconnectPeer(peer.Connection);
                return;
            }

            SpawnRemotePlayer(peer, userName);
        }

        void SpawnRemotePlayer(AbstractNetPeer abstractPeer, string userName)
        {
            var netPlayer = ServerEm.AddPlayer(abstractPeer);
            if (netPlayer == null)
            {
                Log("AddPlayer failed (max players?)");
                return;
            }

            var spawn = FindDefaultSpawn();
            var pawn = ServerEm.AddEntity<ActPlayer>(e =>
            {
                e.Spawn(spawn);
                e.SetDriveLocally(false);
            });
            ServerEm.AddController<ActPlayerController>(netPlayer, pawn);
            Log($"Spawned remote ActPlayer id={pawn.Id} for {userName}");
        }

        // ═══════════════════════════════════════════════════
        // shared helpers
        // ═══════════════════════════════════════════════════

        static Vector3 FindDefaultSpawn()
        {
            var t = GameObject.Find("PlayerSpawn");
            if (t != null) return t.transform.position;
            var origin = new Vector3(0f, 50f, 0f);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 200f))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(0f, 0.05f, 0f);
        }

        void EnsureViewRoot(string levelScene)
        {
            if (_viewRoot != null) return;
            var go = new GameObject("LES_MonsterViews");
            _viewRoot = go.transform;
            MoveToLevel(go, levelScene);
        }

        void ClearViews()
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != null)
                    UnityEngine.Object.Destroy(_views[i].gameObject);
            _views.Clear();
            if (_viewRoot != null)
            {
                UnityEngine.Object.Destroy(_viewRoot.gameObject);
                _viewRoot = null;
            }
        }

        void SyncMonsterViews()
        {
            EntityManager em = (EntityManager)ServerEm ?? ClientEm;
            if (em == null) return;

            if (ClientEm != null && _viewRoot == null && !string.IsNullOrEmpty(_levelScene))
                EnsureViewRoot(_levelScene);
            else if (ClientEm != null && _viewRoot == null)
                EnsureViewRoot(SceneManager.GetActiveScene().name);

            foreach (var monster in em.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed) continue;
                var view = FindView(monster.Id);
                if (view == null)
                {
                    if (_viewRoot == null)
                        EnsureViewRoot(_levelScene ?? "Map1");
                    view = MonsterView.Create(monster.Id, monster.Position, _viewRoot);
                    MoveToLevel(view.gameObject, _levelScene ?? "Map1");
                    _views.Add(view);
                }
                view.Apply(monster.Position, monster.Yaw, monster.SpeedXZ);
            }
        }

        MonsterView FindView(ushort id)
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != null && _views[i].EntityId == id)
                    return _views[i];
            return null;
        }

        void Log(string msg)
        {
            Debug.Log("[LES-Net] " + msg);
            OnLog?.Invoke(msg);
        }

        static Vector3 SnapToGround(Vector3 pos)
        {
            var origin = pos + Vector3.up * 20f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }

        static void MoveToLevel(GameObject go, string sceneName)
        {
            if (go == null || string.IsNullOrEmpty(sceneName)) return;
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (go.scene == scene) return;
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        // ─── INetEventListener (UDP only) ────────────────────

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            Log($"Peer connected {peer.Address}:{peer.Port}");
            if (Role == NetRole.Client)
            {
                _serverPeer = peer;
                var typesMap = LesTypesMapFactory.Create();
                TypesHash = typesMap.EvaluateEntityClassDataHash();

                _writer.Reset();
                _writer.Put((byte)LesPacketType.Serialized);
                _packetProcessor.Write(_writer, new JoinPacket
                {
                    UserName = _userName,
                    GameHash = TypesHash
                });
                peer.Send(_writer, DeliveryMethod.ReliableOrdered);

                var abstractPeer = new GameActNetPeer(peer, true);
                ClientEm = new ClientEntityManager(
                    typesMap,
                    abstractPeer,
                    LesTypesMapFactory.HeaderByte);
                ServerEm = null;

                IsConnected = true;
                StatusText = $"LES Client connected → {peer.Address}";
                Log(StatusText);
                OnConnected?.Invoke();
            }
            else if (Role == NetRole.Host)
            {
                if (peer.Tag == null)
                    peer.Tag = new GameActNetPeer(peer, true);
            }
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Log($"Peer disconnected reason={disconnectInfo.Reason}");
            if (Role == NetRole.Client)
            {
                IsConnected = false;
                StatusText = "Disconnected: " + disconnectInfo.Reason;
                ClientEm = null;
                _serverPeer = null;
                if (_manager != null)
                {
                    _manager.Stop();
                    _manager = null;
                }
                Role = NetRole.None;
                OnDisconnected?.Invoke();
            }
            else
            {
                StatusText = $"Host peers={_manager?.ConnectedPeersCount ?? 0}";
            }
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            Log($"Net error {endPoint}: {socketError}");
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes < 1) return;
            var packetType = (LesPacketType)reader.PeekByte();

            switch (packetType)
            {
                case LesPacketType.EntitySystem:
                {
                    var span = ToSpan(reader);
                    if (Role == NetRole.Host && ServerEm != null && peer.Tag is AbstractNetPeer ap)
                        ServerEm.Deserialize(ap, span);
                    else if (Role == NetRole.Client && ClientEm != null)
                        ClientEm.Deserialize(span);
                    break;
                }
                case LesPacketType.Serialized:
                {
                    reader.GetByte();
                    if (Role == NetRole.Host)
                        _packetProcessor?.ReadAllPackets(reader, peer);
                    break;
                }
                default:
                    Log("Unhandled packet " + packetType);
                    break;
            }
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            if (request.Data.GetString() == ConnectKey)
                request.Accept();
            else
                request.Reject();
        }

        static ReadOnlySpan<byte> ToSpan(NetPacketReader reader)
        {
            int len = reader.AvailableBytes;
            if (len <= 0) return ReadOnlySpan<byte>.Empty;
            var buf = new byte[len];
            reader.GetBytes(buf, len);
            return buf;
        }

        /// <summary>取本机局域网 IPv4，供写入 Steam Lobby（UDP 回退）。</summary>
        public static string GetLanIPv4()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch { /* ignore */ }
            return "127.0.0.1";
        }

        /// <summary>当前进程 SteamID64；未初始化返回 0。</summary>
        public static ulong GetLocalSteamId()
        {
            try
            {
                if (!Steamworks.SteamAPI.IsSteamRunning()) return 0;
                return Steamworks.SteamUser.GetSteamID().m_SteamID;
            }
            catch
            {
                return 0;
            }
        }
    }
}
