using System;
using System.Net;
using System.Net.Sockets;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
using GameAct.Les.Shared;
using GameAct.Spatial;
using GameAct.Les.Transport;

namespace GameAct.Les.Server
{
    /// <summary>
    /// 独立权威服务器（对齐 LiteEntitySystem Example 的 ServerLogic）。
    /// - 可挂在 Unity 场景里做 Listen Server / 本机 Host
    /// - 也可 UNITY_SERVER / -batchmode 跑在 VPS 上做官服 / 专服
    /// 客户端通过官方 launcher 拿到 IP:Port 后 Connect，不依赖本进程里的 ServerEm。
    /// </summary>
    public sealed class LesServerHost : MonoBehaviour, INetEventListener
    {
        public const int DefaultPort = 9050;
        public const int DefaultMonsterCount = 12;
        const string ConnectKey = "game-act-les";

        [Header("Listen")]
        [SerializeField] int _port = DefaultPort;
        [SerializeField] int _monsterCount = DefaultMonsterCount;
        [SerializeField] Vector3 _spawnCenter = new Vector3(0f, 0.05f, 0f);

        [Header("Simulation (dev only)")]
        [SerializeField] bool _simulateLatency;
        [SerializeField] int _simMinLatencyMs = 50;
        [SerializeField] int _simMaxLatencyMs = 80;

        NetManager _manager;
        NetPacketProcessor _packetProcessor;
        ServerEntityManager _em;
        ulong _typesHash;
        bool _started;
        bool _monstersSpawned;

        public bool IsStarted => _started;
        public ServerEntityManager EntityManager => _em;
        public ushort Tick => _em?.Tick ?? 0;
        public int PeerCount => _manager?.ConnectedPeersCount ?? 0;
        public ulong TypesHash => _typesHash;

        public event Action<string> OnLog;

        void Awake()
        {
            LesTypesMapFactory.EnsureFieldTypes();
        }

        /// <summary>代码启动（推荐 VPS / 自动化）。</summary>
        public bool StartServer(int port = -1, int monsterCount = -1, Vector3? spawnCenter = null)
        {
            if (_started) StopServer();

            if (port > 0) _port = port;
            if (monsterCount >= 0) _monsterCount = monsterCount;
            if (spawnCenter.HasValue) _spawnCenter = spawnCenter.Value;

#if UNITY_SERVER
            Application.targetFrameRate = LesTypesMapFactory.TickRate;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
#endif

            var typesMap = LesTypesMapFactory.Create();
            _typesHash = typesMap.EvaluateEntityClassDataHash();

            _packetProcessor = new NetPacketProcessor();
            _packetProcessor.SubscribeReusable<JoinPacket, NetPeer>(OnJoinReceived);

            _manager = new NetManager(this)
            {
                AutoRecycle = true,
                DisconnectTimeout = 15000,
                PacketPoolSize = 1000
            };
            if (_simulateLatency)
            {
                _manager.SimulateLatency = true;
                _manager.SimulationMinLatency = _simMinLatencyMs;
                _manager.SimulationMaxLatency = _simMaxLatencyMs;
            }

            if (!_manager.Start(_port))
            {
                Log($"Listen failed on port {_port}");
                _manager = null;
                return false;
            }

            _em = new ServerEntityManager(
                typesMap,
                LesTypesMapFactory.HeaderByte,
                (byte)LesTypesMapFactory.TickRate,
                ServerSendRate.EqualToFPS);

            SpawnMonsters(_spawnCenter, _monsterCount);
            _started = true;
            Log($"LES Dedicated Server listening :{_port} hash={_typesHash:X} monsters={_monsterCount}");
            return true;
        }

        /// <summary>Inspector / 场景里直接跑时自动监听。</summary>
        void Start()
        {
            // 仅当挂在场景且未手动 StartServer 时自动起（方便本地测）
            if (!_started && Application.isPlaying)
                StartServer();
        }

        void Update()
        {
            if (!_started) return;
            _manager?.PollEvents();

            if (_em != null)
            {
                Vector3 target = default;
                bool found = false;
                foreach (var pl in _em.GetEntities<ActPlayer>())
                {
                    if (pl == null || pl.IsDestroyed) continue;
                    target = pl.Position;
                    found = true;
                    break;
                }
                if (found)
                    FlowFieldService.Tick(target, UnityEngine.Time.deltaTime);
            }

            _em?.Update();
        }

        public void StopServer()
        {
            if (_manager != null)
            {
                _manager.Stop();
                _manager = null;
            }
            _em = null;
            _packetProcessor = null;
            _monstersSpawned = false;
            FlowFieldService.Reset();
            _started = false;
            Log("Server stopped");
        }

        void OnDestroy() => StopServer();

        void SpawnMonsters(Vector3 center, int count)
        {
            if (_em == null || _monstersSpawned) return;
            int n = Mathf.Clamp(count, 0, 64);
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)Mathf.Max(1, n)) * Mathf.PI * 2f;
                float radius = 6f + (i % 3) * 2.5f;
                var pos = SnapToGround(center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
                var monster = _em.AddEntity<ActMonster>(e => e.Spawn(pos));
                _em.AddAIController<MonsterBotController>(c => c.StartControl(monster));
            }
            _monstersSpawned = true;
            FlowFieldService.Ensure(center, halfExtent: 48f, cellSize: FlowField.DefaultCellSize);
            FlowFieldService.Tick(center, 0f);
            Log($"Spawned {n} monsters flowField=on");
        }

        void OnJoinReceived(JoinPacket join, NetPeer peer)
        {
            Log($"Join from {peer} user={join.UserName} hash={join.GameHash:X}");
            if (_em == null)
            {
                peer.Disconnect();
                return;
            }
            if (join.GameHash != _typesHash)
            {
                Log("Types hash mismatch → disconnect");
                peer.Disconnect();
                return;
            }

            var abstractPeer = peer.Tag as AbstractNetPeer ?? new GameActNetPeer(peer, true);
            if (peer.Tag == null)
                peer.Tag = abstractPeer;

            // LES 玩家槽
            var serverPlayer = _em.AddPlayer(abstractPeer);

            // 出生点附近随机一点
            var pos = SnapToGround(_spawnCenter + new Vector3(
                UnityEngine.Random.Range(-3f, 3f), 0f, UnityEngine.Random.Range(-3f, 3f)));

            var pawn = _em.AddEntity<ActPlayer>(e =>
            {
                e.Spawn(pos);
                e.SetDriveLocally(false); // 远端由 Controller 驱动
            });
            _em.AddController<ActPlayerController>(serverPlayer, pawn);
            Log($"Spawned player id={pawn.Id} for {join.UserName}");
        }

        // ---- INetEventListener ----

        public void OnPeerConnected(NetPeer peer) => Log($"Peer connected: {peer}");

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            Log($"Peer disconnected: {info.Reason}");
            if (peer.Tag is AbstractNetPeer ap && _em != null)
                _em.RemovePlayer(ap);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            => Log($"NetworkError: {socketError}");

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (reader.AvailableBytes < 1) return;
            byte packetType = reader.PeekByte();
            switch ((LesPacketType)packetType)
            {
                case LesPacketType.EntitySystem:
                    if (peer.Tag is AbstractNetPeer ap)
                        _em?.Deserialize(ap, reader.GetRemainingBytesSpan());
                    break;
                case LesPacketType.Serialized:
                    reader.GetByte(); // consume type
                    _packetProcessor.ReadAllPackets(reader, peer);
                    break;
                default:
                    Log($"Unhandled packet type: {packetType}");
                    break;
            }
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.AcceptIfKey(ConnectKey);
        }

        static Vector3 SnapToGround(Vector3 pos)
        {
            var origin = pos + Vector3.up * 20f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return new Vector3(pos.x, Mathf.Max(pos.y, 0.05f), pos.z);
        }

        void Log(string msg)
        {
            Debug.Log("[LES-Server] " + msg);
            OnLog?.Invoke(msg);
        }
    }
}
