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
using GameAct.Skill;
using GameAct.Les.Transport;
using GameAct.Les.View;
using GameAct.Spatial;
using GameAct.Net;
using GameAct.Gameplay.Player;

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
        readonly List<PlayerView> _playerViews = new List<PlayerView>(8);
        Transform _viewRoot;
        Transform _playerViewRoot;
        string _levelScene;
        bool _suppressDisconnectEvent;
        string _userName = "Player";
        bool _enemiesSpawned;

        // 怪物 View 清理用（每帧复用，避免分配）
        readonly HashSet<ushort> _aliveMonsterIds = new HashSet<ushort>();
        // Host：远程玩家 peer → Pawn。退房（断线）时据此销毁角色，并防止重复 Join 刷出第二个角色。
        readonly Dictionary<AbstractNetPeer, ActPlayer> _remotePawns = new Dictionary<AbstractNetPeer, ActPlayer>();

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
            MonsterDamageService.Clear(); // Host 本地结算，不是 Client 代理
            MonsterDeathService.AuthorityDestroyMonster = DestroyMonsterById;
            MonsterKnockbackService.AuthorityKnockback = KnockbackMonsterById;

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

        public void Disconnect() => DisconnectInternal(softSteam: false);

        /// <summary>
        /// Editor Stop / 进程退出用：托管状态全清，Steam P2P 只 SoftDispose（不调 CloseConnection）。
        /// 避免 Invalid pipe handle → Native Access Violation 闪退。
        /// </summary>
        public void DisconnectSoft() => DisconnectInternal(softSteam: true);

        void DisconnectInternal(bool softSteam)
        {
            // 编辑器 Stop / Domain unload：避免回调与二次 Destroy 把 Editor 打崩
            bool quitting = softSteam || !Application.isPlaying;
            _suppressDisconnectEvent = quitting;

            try
            {
                if (ServerEm != null)
                {
                    MonsterDeathService.Clear();
                    MonsterKnockbackService.Clear();
                }
                MonsterDamageService.Clear();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LES-Net] MonsterService.Clear: " + e.Message);
            }

            try { ClearViews(); }
            catch (Exception e) { Debug.LogWarning("[LES-Net] ClearViews: " + e.Message); }

            if (_steam != null)
            {
                try
                {
                    _steam.OnPeerConnected -= OnSteamPeerConnected;
                    _steam.OnPeerDisconnected -= OnSteamPeerDisconnected;
                    _steam.OnDataReceived -= OnSteamDataReceived;
                    _steam.OnLog -= OnSteamLog;
                    if (softSteam || !Application.isPlaying)
                        _steam.SoftDispose();
                    else
                        _steam.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LES-Net] Steam dispose: " + e.Message);
                }
                _steam = null;
            }
            _steamServerPeer = null;

            if (_manager != null)
            {
                try { _manager.Stop(); }
                catch (Exception e) { Debug.LogWarning("[LES-Net] NetManager.Stop: " + e.Message); }
                _manager = null;
            }
            _serverPeer = null;
            ServerEm = null;
            ClientEm = null;
            _packetProcessor = null;
            _enemiesSpawned = false;
            _remotePawns.Clear();
            _aliveMonsterIds.Clear();
            try { FlowFieldService.Reset(); } catch { /* ignore */ }
            _activeTransport = NetTransportKind.Udp;

            var was = IsConnected || Role != NetRole.None;
            IsConnected = false;
            Role = NetRole.None;
            StatusText = "Disconnected";
            if (was && !_suppressDisconnectEvent)
            {
                try { OnDisconnected?.Invoke(); }
                catch (Exception e) { Debug.LogWarning("[LES-Net] OnDisconnected: " + e.Message); }
            }
            _suppressDisconnectEvent = false;
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
            if (!Application.isPlaying) return;
            if (Role == NetRole.None && !IsConnected) return;

            if (_activeTransport == NetTransportKind.SteamP2P)
                _steam?.Poll();
            else
                _manager?.PollEvents();

            // Host 权威：以第一个存活玩家为 Flow Field 目标
            if (ServerEm != null)
            {
                Vector3 target = default;
                bool found = false;
                foreach (var pl in ServerEm.GetEntities<ActPlayer>())
                {
                    if (pl == null || pl.IsDestroyed) continue;
                    target = pl.Position;
                    found = true;
                    break;
                }
                if (found)
                    FlowFieldService.Tick(target, 1f / 30f);
            }

            ServerEm?.Update();
            ClientEm?.Update();
            SyncMonsterViews();
            SyncPlayerViews();
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

            // Host 刷怪时初始化 Flow Field
            FlowFieldService.Ensure(center, halfExtent: 48f, cellSize: FlowField.DefaultCellSize);
            FlowFieldService.Tick(center, 0f);

            Log($"Spawned {n} LES enemies around {center} flowField=on");
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
                InstallClientCombatBridge();

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
                MonsterDamageService.Clear();
                // Poll 在 Role=None 后不再跑，这里直接清掉所有远程表现，避免残留
                try { ClearViews(); }
                catch (Exception e) { Debug.LogWarning("[LES-Net] ClearViews: " + e.Message); }
                OnDisconnected?.Invoke();
            }
            else if (Role == NetRole.Host && ServerEm != null)
            {
                // 退房：销毁该玩家的角色 / 控制器 / 玩家槽，其余端的远程 PlayerView 随实体消失自动回收
                RemoveRemotePlayer(peer, reason);
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
                case LesPacketType.MonsterDamage:
                case LesPacketType.MonsterKnockback:
                {
                    if (Role == NetRole.Host)
                        HandleClientCombatPacket(data);
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
            // 重复 Join（重发 / 重连抖动）：不再刷第二个角色
            if (_remotePawns.ContainsKey(abstractPeer))
            {
                Log($"Duplicate Join ignored user={userName}");
                return;
            }

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
            _remotePawns[abstractPeer] = pawn;
            Log($"Spawned remote ActPlayer id={pawn.Id} for {userName}");
        }

        /// <summary>
        /// Host：玩家退房 / 断线 → 销毁其 Pawn、控制器与玩家槽。
        /// Pawn 在 ServerEm 销毁后会同步给所有 Client，各端 SyncPlayerViews 随之回收远程 PlayerView。
        /// </summary>
        void RemoveRemotePlayer(AbstractNetPeer peer, string reason)
        {
            if (peer == null) return;

            _remotePawns.TryGetValue(peer, out var pawn);
            _remotePawns.Remove(peer);

            var em = ServerEm;
            if (em == null) return;

            // 1) Pawn（最关键，先做）
            try
            {
                if (pawn != null && !pawn.IsDestroyed)
                    pawn.Destroy();
            }
            catch (Exception e) { Debug.LogWarning("[LES-Net] destroy pawn: " + e.Message); }

            // 2) 控制器：Pawn 已销毁/为空，或正是该 Pawn 的控制器
            try
            {
                var stale = new List<ActPlayerController>(2);
                foreach (var c in em.GetEntities<ActPlayerController>())
                {
                    if (c == null || c.IsDestroyed) continue;
                    var p = c.Pawn;
                    if (p == null || p.IsDestroyed || (pawn != null && p.Id == pawn.Id))
                        stale.Add(c);
                }
                for (int i = 0; i < stale.Count; i++)
                    stale[i].Destroy();
            }
            catch (Exception e) { Debug.LogWarning("[LES-Net] destroy controller: " + e.Message); }

            // 3) 玩家槽
            try { em.RemovePlayer(peer); }
            catch (Exception e) { Debug.LogWarning("[LES-Net] RemovePlayer: " + e.Message); }

            Log($"Remote player removed pawn={(pawn != null ? pawn.Id.ToString() : "?")} reason={reason}");
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
            SafeDestroyViews(_views);
            _views.Clear();
            SafeDestroyPlayerViews();
            if (_viewRoot != null)
            {
                SafeDestroyGo(_viewRoot.gameObject);
                _viewRoot = null;
            }
            if (_playerViewRoot != null)
            {
                SafeDestroyGo(_playerViewRoot.gameObject);
                _playerViewRoot = null;
            }
        }

        static void SafeDestroyGo(GameObject go)
        {
            if (go == null) return;
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(go);
            else
                UnityEngine.Object.Destroy(go);
        }

        static void SafeDestroyViews(List<MonsterView> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                    SafeDestroyGo(list[i].gameObject);
            }
        }

        void SafeDestroyPlayerViews()
        {
            for (int i = 0; i < _playerViews.Count; i++)
            {
                if (_playerViews[i] != null)
                    SafeDestroyGo(_playerViews[i].gameObject);
            }
            _playerViews.Clear();
        }

        void SyncMonsterViews()
        {
            EntityManager em = (EntityManager)ServerEm ?? ClientEm;
            if (em == null) return;

            if (ClientEm != null && _viewRoot == null && !string.IsNullOrEmpty(_levelScene))
                EnsureViewRoot(_levelScene);
            else if (ClientEm != null && _viewRoot == null)
                EnsureViewRoot(SceneManager.GetActiveScene().name);

            // 1) 当前存活的怪物实体
            _aliveMonsterIds.Clear();
            foreach (var monster in em.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed || monster.IsDead) continue;
                _aliveMonsterIds.Add(monster.Id);
            }

            // 2) 清理：实体已消失（= 被 Host 销毁）的 View → 播死亡并回收。
            //    先清理再建新 View：LES 实体 Id 可能被复用，不能让旧 View 占着 Id。
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                var v = _views[i];
                if (v == null)
                {
                    _views.RemoveAt(i);
                    continue;
                }
                if (_aliveMonsterIds.Contains(v.EntityId)) continue;

                // Host 本地击杀的 View 已在 dying（幂等）；Client 靠这里同步销毁
                v.BeginRemoteDeath();
                _views.RemoveAt(i); // 由 View 自己延迟回收，不再占用 Id
            }

            // 3) 为存活怪物建 View / 应用位姿
            foreach (var monster in em.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed || monster.IsDead) continue;
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

        /// <summary>
        /// 远程玩家表现：本地玩家由 GameplayRunner 建 Player_Local；
        /// 这里只为「非本机控制」的 ActPlayer 建 Player_{id}。
        /// </summary>
        void SyncPlayerViews()
        {
            EntityManager em = (EntityManager)ServerEm ?? ClientEm;
            if (em == null) return;

            if (_playerViewRoot == null)
            {
                var level = !string.IsNullOrEmpty(_levelScene)
                    ? _levelScene
                    : SceneManager.GetActiveScene().name;
                EnsurePlayerViewRoot(level);
            }
            else
            {
                // Map1 稍后才加载时，根节点补一次搬迁（失败不抛）
                TryMoveToLevel(_playerViewRoot.gameObject, _levelScene ?? "Map1");
            }

            var alive = new HashSet<int>();
            foreach (var pl in em.GetEntities<ActPlayer>())
            {
                if (pl == null || pl.IsDestroyed) continue;
                // 本机控制的交给 GameplayRunner（Player_Local）
                if (pl.DriveLocally || pl.IsLocalControlled) continue;

                int id = pl.Id;
                alive.Add(id);
                var view = FindPlayerView(id);
                Vector3 pos = pl.InterpolatedPosition;
                float yaw = pl.InterpolatedYaw;

                if (view == null)
                {
                    if (_playerViewRoot == null)
                        EnsurePlayerViewRoot(_levelScene ?? "Map1");

                    // 先入表再摆场景，避免 Move 抛异常导致下一帧重复 Create
                    view = PlayerView.Create(id, isLocal: false, pos, _playerViewRoot);
                    _playerViews.Add(view);
                    Log($"Remote PlayerView created id={id}");
                    // 子物体跟根节点同场景即可，不要对每个子物体 Move（会 ArgumentException 刷屏）
                }

                if (view == null) continue;

                // 远程 ActPlayer 在非权威端不跑 Update，_velocity/_grounded 是初值；
                // 动画必须用权威端同步下来的 Anim* / Count，不能读 Velocity/Grounded。
                view.SyncEvents(pl.JumpCount, pl.AttackCount);
                view.ApplyPose(pos, yaw, pl.AnimSpeedXZ, pl.AnimGrounded, pl.AnimVelY);
            }

            for (int i = _playerViews.Count - 1; i >= 0; i--)
            {
                var pv = _playerViews[i];
                if (pv == null || !alive.Contains(pv.EntityId))
                {
                    if (pv != null)
                        SafeDestroyGo(pv.gameObject);
                    _playerViews.RemoveAt(i);
                }
            }
        }

        void EnsurePlayerViewRoot(string levelScene)
        {
            if (_playerViewRoot != null) return;
            var go = new GameObject("LES_PlayerViews");
            UnityEngine.Object.DontDestroyOnLoad(go); // 避免跟 Boot/Map 加载来回搬导致异常
            _playerViewRoot = go.transform;
            TryMoveToLevel(go, levelScene);
        }

        PlayerView FindPlayerView(int entityId)
        {
            for (int i = 0; i < _playerViews.Count; i++)
            {
                var pv = _playerViews[i];
                if (pv != null && pv.EntityId == entityId)
                    return pv;
            }
            // 列表丢了但场景里还在（异常中断过）：认领，禁止再 Create
            if (_playerViewRoot != null)
            {
                for (int i = 0; i < _playerViewRoot.childCount; i++)
                {
                    var pv = _playerViewRoot.GetChild(i).GetComponent<PlayerView>();
                    if (pv != null && pv.EntityId == entityId)
                    {
                        _playerViews.Add(pv);
                        return pv;
                    }
                }
            }
            return null;
        }

        void DestroyMonsterById(int entityId)
        {
            if (ServerEm == null) return;
            foreach (var monster in ServerEm.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed) continue;
                if (monster.Id != entityId) continue;
                monster.MarkDead();
                monster.Destroy();
                Debug.Log($"[LES-Net] ActMonster destroyed id={entityId}");
                break;
            }

            // AI 控制器随怪物一起清掉（Pawn 已销毁/为空的残留）
            try
            {
                List<MonsterBotController> stale = null;
                foreach (var c in ServerEm.GetEntities<MonsterBotController>())
                {
                    if (c == null || c.IsDestroyed) continue;
                    var p = c.Pawn;
                    if (p == null || p.IsDestroyed)
                    {
                        if (stale == null) stale = new List<MonsterBotController>(2);
                        stale.Add(c);
                    }
                }
                if (stale != null)
                    for (int i = 0; i < stale.Count; i++)
                        stale[i].Destroy();
            }
            catch (Exception e) { Debug.LogWarning("[LES-Net] destroy bot controller: " + e.Message); }
        }

        /// <summary>权威击退（Host 本地命中 / Host 收到 Client 上报）。</summary>
        void KnockbackMonsterById(int entityId, Vector3 worldDir, float distance)
        {
            if (ServerEm == null || distance <= 0.001f) return;
            foreach (var monster in ServerEm.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed || monster.IsDead) continue;
                if (monster.Id != entityId) continue;
                monster.ApplyKnockback(worldDir, distance);
                break;
            }
        }

        // ═══════════════════════════════════════════════════
        // Client → Host 战斗上报（怪物 HP / 死亡 / 击退只由 Host 结算）
        // ═══════════════════════════════════════════════════

        void InstallClientCombatBridge()
        {
            MonsterDamageService.ClientDamageSender = SendMonsterDamageToHost;
            MonsterDamageService.ClientKnockbackSender = SendMonsterKnockbackToHost;
        }

        void SendMonsterDamageToHost(int entityId, float damage)
        {
            // [type][id:u16][damage:f32]
            var buf = new byte[1 + 2 + 4];
            buf[0] = (byte)LesPacketType.MonsterDamage;
            buf[1] = (byte)(entityId & 0xff);
            buf[2] = (byte)((entityId >> 8) & 0xff);
            Buffer.BlockCopy(BitConverter.GetBytes(damage), 0, buf, 3, 4);
            SendToHost(buf);
        }

        void SendMonsterKnockbackToHost(int entityId, Vector3 worldDir, float distance)
        {
            // [type][id:u16][dirX:f32][dirZ:f32][dist:f32]
            var buf = new byte[1 + 2 + 4 + 4 + 4];
            buf[0] = (byte)LesPacketType.MonsterKnockback;
            buf[1] = (byte)(entityId & 0xff);
            buf[2] = (byte)((entityId >> 8) & 0xff);
            Buffer.BlockCopy(BitConverter.GetBytes(worldDir.x), 0, buf, 3, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(worldDir.z), 0, buf, 7, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(distance), 0, buf, 11, 4);
            SendToHost(buf);
        }

        /// <summary>Client → Host 可靠有序发送（UDP / Steam 通用）。</summary>
        void SendToHost(byte[] data)
        {
            if (Role != NetRole.Client || data == null || data.Length == 0) return;

            if (_activeTransport == NetTransportKind.SteamP2P)
            {
                _steamServerPeer?.SendReliableOrdered(data);
                return;
            }

            if (_serverPeer == null) return;
            var w = new NetDataWriter();
            w.Put(data);
            _serverPeer.Send(w, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>Host：处理 Client 上报的怪物伤害 / 击退。数据来自网络，一律校验。</summary>
        void HandleClientCombatPacket(byte[] d)
        {
            if (ServerEm == null || d == null || d.Length < 3) return;

            var type = (LesPacketType)d[0];
            int id = d[1] | (d[2] << 8);

            if (type == LesPacketType.MonsterDamage)
            {
                if (d.Length < 7) return;
                float dmg = BitConverter.ToSingle(d, 3);
                if (float.IsNaN(dmg) || dmg <= 0f || dmg > 1000f) return;

                if (CombatTargetRegistry.TryGetReceiver(id, out var recv) && !recv.IsDead)
                    recv.ApplyDamage(dmg, "client"); // 死亡 → MonsterView.OnReceiverDied → DestroyMonsterById → 同步给所有端
            }
            else if (type == LesPacketType.MonsterKnockback)
            {
                if (d.Length < 15) return;
                float dx = BitConverter.ToSingle(d, 3);
                float dz = BitConverter.ToSingle(d, 7);
                float dist = BitConverter.ToSingle(d, 11);
                if (float.IsNaN(dx) || float.IsNaN(dz) || float.IsNaN(dist)) return;
                if (dist <= 0.001f || dist > 10f) return;

                // Client 侧已按 HitReceiver 抗性算好最终距离，这里直接走权威击退
                MonsterKnockbackService.RequestKnockback(id, new Vector3(dx, 0f, dz), dist);
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
            TryMoveToLevel(go, sceneName);
        }

        /// <summary>永不抛：场景未就绪 / DDOL / 父子跨场景时静默跳过。</summary>
        static bool TryMoveToLevel(GameObject go, string sceneName)
        {
            if (go == null || string.IsNullOrEmpty(sceneName)) return false;
            try
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                if (!scene.IsValid() || !scene.isLoaded) return false;
                if (go.scene == scene) return true;
                // 有父节点时先脱父，再搬（避免部分 Unity 版本 ArgumentException）
                if (go.transform.parent != null)
                    go.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(go, scene);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LES-Net] MoveToLevel skip: " + e.Message);
                return false;
            }
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
                InstallClientCombatBridge();

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
                MonsterDamageService.Clear();
                // Poll 在 Role=None 后不再跑，这里直接清掉所有远程表现，避免残留
                try { ClearViews(); }
                catch (Exception e) { Debug.LogWarning("[LES-Net] ClearViews: " + e.Message); }
                OnDisconnected?.Invoke();
            }
            else
            {
                // 退房：销毁该玩家的角色 / 控制器 / 玩家槽，其余端的远程 PlayerView 随实体消失自动回收
                if (Role == NetRole.Host && peer.Tag is AbstractNetPeer ap)
                    RemoveRemotePlayer(ap, disconnectInfo.Reason.ToString());
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
                case LesPacketType.MonsterDamage:
                case LesPacketType.MonsterKnockback:
                {
                    // 整包读出（含 type 字节）
                    var data = new byte[reader.AvailableBytes];
                    reader.GetBytes(data, data.Length);
                    if (Role == NetRole.Host)
                        HandleClientCombatPacket(data);
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
