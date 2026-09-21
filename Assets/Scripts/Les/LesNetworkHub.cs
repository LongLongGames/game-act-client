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
    /// LES 传输中枢：LiteNetLib 收发 + Server/Client EntityManager。
    /// 实现 INetSession，替换「只起 UDP、不接 LES」的缺口。
    /// Solo 不走本类（仍用 LesAuthoritySession 离线权威）。
    /// </summary>
    public sealed class LesNetworkHub : INetSession, INetEventListener, IDisposable
    {
        public const int DefaultPort = 9050;
        public const int DefaultEnemyCount = 12;
        const string ConnectKey = "game-act-les";

        public NetRole Role { get; private set; } = NetRole.None;
        public bool IsConnected { get; private set; }
        public int PeerCount => _manager?.ConnectedPeersCount ?? 0;
        public string StatusText { get; private set; } = "Idle";

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnLog;

        public ServerEntityManager ServerEm { get; private set; }
        public ClientEntityManager ClientEm { get; private set; }
        public ulong TypesHash { get; private set; }

        NetManager _manager;
        NetPeer _serverPeer;
        NetPacketProcessor _packetProcessor;
        readonly NetDataWriter _writer = new NetDataWriter();
        readonly List<EnemyView> _views = new List<EnemyView>(32);
        Transform _viewRoot;
        string _levelScene;
        string _userName = "Player";
        bool _enemiesSpawned;

        // 可选延迟模拟（本机测预测用，默认关）
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
            if (transport != NetTransportKind.Udp && transport != NetTransportKind.InProcess)
                Log("SteamP2P 尚未接入，当前用 UDP");

            var typesMap = LesTypesMapFactory.Create();
            TypesHash = typesMap.EvaluateEntityClassDataHash();
            ServerEm = new ServerEntityManager(
                typesMap,
                LesTypesMapFactory.HeaderByte,
                (byte)LesTypesMapFactory.TickRate,
                ServerSendRate.EqualToFPS);
            ClientEm = null;

            _packetProcessor = new NetPacketProcessor();
            _packetProcessor.SubscribeReusable<JoinPacket, NetPeer>(OnJoinReceived);

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
            StatusText = $"LES Host :{port} hash={TypesHash:X}";
            Log(StatusText);
            OnConnected?.Invoke();
            await UniTask.Yield();
            return true;
        }

        public async UniTask<bool> ConnectAsync(string address, int port = DefaultPort, NetTransportKind transport = NetTransportKind.Udp)
        {
            Disconnect();
            if (string.IsNullOrWhiteSpace(address))
            {
                StatusText = "地址为空";
                return false;
            }

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
            StatusText = $"Connecting {address}:{port}…";
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

        public void Disconnect()
        {
            ClearViews();
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

            var was = IsConnected || Role != NetRole.None;
            IsConnected = false;
            Role = NetRole.None;
            StatusText = "Disconnected";
            if (was) OnDisconnected?.Invoke();
        }

        public void SendReliable(byte[] data)
        {
            // 预留；实体同步由 LES Manager 自行发送
            if (data == null || data.Length == 0 || _manager == null) return;
            var w = new NetDataWriter();
            w.Put(data);
            if (Role == NetRole.Host)
                _manager.SendToAll(w, DeliveryMethod.ReliableOrdered);
            else
                _serverPeer?.Send(w, DeliveryMethod.ReliableOrdered);
        }

        public void Poll()
        {
            _manager?.PollEvents();
            ServerEm?.Update();
            ClientEm?.Update();
            SyncEnemyViews();
        }

        public void Dispose() => Disconnect();

        /// <summary>Host/Solo 权威刷怪；Client 靠快照构造。</summary>
        public void SpawnEnemiesAround(Vector3 center, string levelScene, int count = DefaultEnemyCount)
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

                var enemy = ServerEm.AddEntity<ActEnemy>(e => e.Spawn(pos));
                ServerEm.AddAIController<EnemyBotController>(c => c.StartControl(enemy));

                var view = EnemyView.Create(enemy.Id, pos, _viewRoot);
                MoveToLevel(view.gameObject, levelScene);
                _views.Add(view);
            }

            _enemiesSpawned = true;
            Log($"Spawned {n} LES enemies around {center}");
        }

        void ApplySimulationSettings(NetManager m)
        {
            m.SimulateLatency = SimulateLatency;
            m.SimulationMinLatency = SimulationMinLatencyMs;
            m.SimulationMaxLatency = SimulationMaxLatencyMs;
            m.SimulatePacketLoss = SimulatePacketLoss;
            m.SimulationPacketLossChance = SimulationPacketLossChance;
        }

        void OnJoinReceived(JoinPacket join, NetPeer peer)
        {
            Log($"Join from {peer} user={join.UserName} hash={join.GameHash:X}");
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

            ServerEm.AddPlayer(abstractPeer);
            // 玩家 Pawn/HumanController 下一步再迁入 LES；当前只注册 NetPlayer 以打通传输
        }

        void EnsureViewRoot(string levelScene)
        {
            if (_viewRoot != null) return;
            var go = new GameObject("LES_EnemyViews");
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

        void SyncEnemyViews()
        {
            EntityManager em = (EntityManager)ServerEm ?? ClientEm;
            if (em == null) return;

            // Client：实体构造后补 View
            if (ClientEm != null && _viewRoot == null && !string.IsNullOrEmpty(_levelScene))
                EnsureViewRoot(_levelScene);
            else if (ClientEm != null && _viewRoot == null)
                EnsureViewRoot(SceneManager.GetActiveScene().name);

            foreach (var enemy in em.GetEntities<ActEnemy>())
            {
                if (enemy == null || enemy.IsDestroyed) continue;
                var view = FindView(enemy.Id);
                if (view == null)
                {
                    if (_viewRoot == null)
                        EnsureViewRoot(_levelScene ?? "Map1");
                    view = EnemyView.Create(enemy.Id, enemy.Position, _viewRoot);
                    MoveToLevel(view.gameObject, _levelScene ?? "Map1");
                    _views.Add(view);
                }
                view.Apply(enemy.Position, enemy.Yaw);
            }
        }

        EnemyView FindView(ushort id)
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

        // ─── INetEventListener ───────────────────────────────

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            Log($"Peer connected {peer.Address}:{peer.Port}");
            if (Role == NetRole.Client)
            {
                _serverPeer = peer;
                var typesMap = LesTypesMapFactory.Create();
                TypesHash = typesMap.EvaluateEntityClassDataHash();

                // Join
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
                // 等 Join 再 AddPlayer
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
                OnDisconnected?.Invoke();
            }
            else if (Role == NetRole.Host && peer.Tag is AbstractNetPeer ap)
            {
                ServerEm?.RemovePlayer(ap);
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
                    reader.GetByte(); // consume type
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
            // 兼容不同 LiteNetLib：优先剩余字节数组
            int len = reader.AvailableBytes;
            if (len <= 0) return ReadOnlySpan<byte>.Empty;
            var buf = new byte[len];
            reader.GetBytes(buf, len);
            return buf;
        }

        /// <summary>取本机局域网 IPv4，供写入 Steam Lobby。</summary>
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
    }
}
