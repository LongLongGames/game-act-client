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

namespace GameAct.Les.Client
{
    /// <summary>
    /// 纯客户端会话（对齐 LiteEntitySystem Example 的 ClientLogic）。
    /// 由官方 Battle.net-like Launcher 启动后，传入官服/专服 IP:Port 连接。
    /// 本类不创建 ServerEntityManager，只做 ClientEntityManager + View。
    /// </summary>
    public sealed class LesClientSession : INetEventListener, IDisposable
    {
        public const int DefaultPort = 9050;
        const string ConnectKey = "game-act-les";

        NetManager _manager;
        NetPeer _serverPeer;
        NetPacketProcessor _packetProcessor;
        ClientEntityManager _em;
        readonly NetDataWriter _writer = new NetDataWriter();
        readonly List<MonsterView> _views = new List<MonsterView>(32);
        Transform _viewRoot;
        string _levelScene = "Map1";
        string _userName = "Player";
        bool _connected;

        public bool IsConnected => _connected;
        public ClientEntityManager EntityManager => _em;
        public string StatusText { get; private set; } = "Idle";

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnLog;

        public void SetUserName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _userName = name.Trim();
        }

        /// <summary>
        /// Launcher / 房间系统调用：连接指定官服或专服。
        /// 例：ConnectAsync("game-act.example.com", 9050)
        /// </summary>
        public async UniTask<bool> ConnectAsync(string address, int port = DefaultPort, string levelScene = null, float timeoutSec = 10f)
        {
            Disconnect();
            if (!string.IsNullOrEmpty(levelScene))
                _levelScene = levelScene;

            LesTypesMapFactory.EnsureFieldTypes();
            var typesMap = LesTypesMapFactory.Create();
            ulong typesHash = typesMap.EvaluateEntityClassDataHash();

            _packetProcessor = new NetPacketProcessor();
            _manager = new NetManager(this)
            {
                AutoRecycle = true,
                DisconnectTimeout = 10000
            };

            if (!_manager.Start())
            {
                StatusText = "NetManager start failed";
                Log(StatusText);
                return false;
            }

            StatusText = $"Connecting {address}:{port}…";
            Log(StatusText);
            _manager.Connect(address, port, ConnectKey);

            float t0 = Time.realtimeSinceStartup;
            while (!_connected && Time.realtimeSinceStartup - t0 < timeoutSec)
            {
                _manager.PollEvents();
                await UniTask.Yield();
            }

            if (!_connected)
            {
                StatusText = "Connect timeout";
                Log(StatusText);
                Disconnect();
                return false;
            }

            // Join
            _writer.Reset();
            _writer.Put((byte)LesPacketType.Serialized);
            _packetProcessor.Write(_writer, new JoinPacket
            {
                UserName = _userName,
                GameHash = typesHash
            });
            _serverPeer.Send(_writer, DeliveryMethod.ReliableOrdered);

            StatusText = $"Connected as {_userName}";
            Log(StatusText);
            return true;
        }

        /// <summary>每帧由 GameplayRunner / 启动器调用。</summary>
        public void Poll()
        {
            _manager?.PollEvents();
            _em?.Update();
            SyncMonsterViews();
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
            _em = null;
            _packetProcessor = null;
            var was = _connected;
            _connected = false;
            StatusText = "Disconnected";
            if (was) OnDisconnected?.Invoke();
        }

        public void Dispose() => Disconnect();

        // ---- INetEventListener ----

        public void OnPeerConnected(NetPeer peer)
        {
            _serverPeer = peer;
            var typesMap = LesTypesMapFactory.Create();
            var abstractPeer = new GameActNetPeer(peer, true);
            _em = new ClientEntityManager(
                typesMap,
                abstractPeer,
                LesTypesMapFactory.HeaderByte);
            _connected = true;
            EnsureViewRoot(_levelScene);
            Log($"Peer connected, ClientEntityManager ready");
            OnConnected?.Invoke();
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            Log($"Disconnected: {info.Reason}");
            _connected = false;
            _em = null;
            _serverPeer = null;
            OnDisconnected?.Invoke();
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
                    _em?.Deserialize(reader.GetRemainingBytesSpan());
                    break;
                case LesPacketType.Serialized:
                    reader.GetByte();
                    _packetProcessor?.ReadAllPackets(reader);
                    break;
                default:
                    Log($"Unhandled packet: {packetType}");
                    break;
            }
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public void OnConnectionRequest(ConnectionRequest request) => request.Reject();

        // ---- View ----

        void EnsureViewRoot(string levelScene)
        {
            if (_viewRoot != null) return;
            var go = new GameObject("LES_MonsterViews_Client");
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
            if (_em == null) return;
            if (_viewRoot == null)
                EnsureViewRoot(_levelScene);

            foreach (var monster in _em.GetEntities<ActMonster>())
            {
                if (monster == null || monster.IsDestroyed) continue;
                var view = FindView(monster.Id);
                if (view == null)
                {
                    view = MonsterView.Create(monster.Id, monster.Position, _viewRoot);
                    MoveToLevel(view.gameObject, _levelScene);
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

        static void MoveToLevel(GameObject go, string levelScene)
        {
            if (string.IsNullOrEmpty(levelScene)) return;
            var scene = SceneManager.GetSceneByName(levelScene);
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.MoveGameObjectToScene(go, scene);
        }

        void Log(string msg)
        {
            Debug.Log("[LES-Client] " + msg);
            OnLog?.Invoke(msg);
        }
    }
}
