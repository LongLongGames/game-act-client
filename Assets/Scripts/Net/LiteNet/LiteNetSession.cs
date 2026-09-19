using System;
using System.Net;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace GameAct.Net.LiteNet
{
    /// <summary>
    /// LiteNetLib 会话。P1：UDP Host/Client 可通；Steam P2P 传输后续替换 NetManager 底层。
    /// </summary>
    public class LiteNetSession : INetSession, INetEventListener, IDisposable
    {
        public NetRole Role { get; private set; } = NetRole.None;
        public bool IsConnected { get; private set; }
        public int PeerCount => _manager?.ConnectedPeersCount ?? 0;
        public string StatusText { get; private set; } = "Idle";

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnLog;

        NetManager _manager;
        NetPeer _serverPeer;
        EventBasedNetListener _listener;

        const string ConnectKey = "game-act-p1";

        public async UniTask<bool> StartHostAsync(int port = 9050, NetTransportKind transport = NetTransportKind.Udp)
        {
            Disconnect();
            if (transport != NetTransportKind.Udp)
            {
                Log("SteamP2P transport 尚未接入，P1 先用 UDP");
            }

            _listener = new EventBasedNetListener();
            WireListener(_listener);
            _manager = new NetManager(_listener)
            {
                AutoRecycle = true,
                DisconnectTimeout = 8000
            };

            if (!_manager.Start(port))
            {
                StatusText = "Host 启动失败 port=" + port;
                Log(StatusText);
                return false;
            }

            Role = NetRole.Host;
            IsConnected = true;
            StatusText = $"Host listening :{port}";
            Log(StatusText);
            OnConnected?.Invoke();
            await UniTask.Yield();
            return true;
        }

        public async UniTask<bool> ConnectAsync(string address, int port = 9050, NetTransportKind transport = NetTransportKind.Udp)
        {
            Disconnect();
            if (string.IsNullOrWhiteSpace(address))
            {
                StatusText = "地址为空";
                return false;
            }

            _listener = new EventBasedNetListener();
            WireListener(_listener);
            _manager = new NetManager(_listener)
            {
                AutoRecycle = true,
                DisconnectTimeout = 8000
            };

            if (!_manager.Start())
            {
                StatusText = "Client NetManager 启动失败";
                Log(StatusText);
                return false;
            }

            Role = NetRole.Client;
            StatusText = $"Connecting {address}:{port} …";
            Log(StatusText);
            _manager.Connect(address, port, ConnectKey);

            // 等最多 5s
            var t0 = Time.realtimeSinceStartup;
            while (!IsConnected && Time.realtimeSinceStartup - t0 < 5f)
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
            if (_manager != null)
            {
                _manager.Stop();
                _manager = null;
            }
            _listener = null;
            _serverPeer = null;
            var was = IsConnected || Role != NetRole.None;
            IsConnected = false;
            Role = NetRole.None;
            StatusText = "Disconnected";
            if (was) OnDisconnected?.Invoke();
        }

        public void SendReliable(byte[] data)
        {
            if (_manager == null || data == null || data.Length == 0) return;
            var writer = new NetDataWriter();
            writer.Put(data);
            if (Role == NetRole.Host)
            {
                _manager.SendToAll(writer, DeliveryMethod.ReliableOrdered);
            }
            else if (_serverPeer != null)
            {
                _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        public void Poll()
        {
            _manager?.PollEvents();
        }

        public void Dispose() => Disconnect();

        void WireListener(EventBasedNetListener l)
        {
            l.PeerConnectedEvent += peer =>
            {
                Log($"Peer connected {peer.Address}:{peer.Port}");
                if (Role == NetRole.Client)
                {
                    _serverPeer = peer;
                    IsConnected = true;
                    StatusText = $"Connected to {peer.Address}";
                    OnConnected?.Invoke();
                }
                else
                {
                    StatusText = $"Host peers={_manager.ConnectedPeersCount}";
                }
            };

            l.PeerDisconnectedEvent += (peer, info) =>
            {
                Log($"Peer disconnected {peer.Address} reason={info.Reason}");
                if (Role == NetRole.Client)
                {
                    IsConnected = false;
                    StatusText = "Disconnected: " + info.Reason;
                    OnDisconnected?.Invoke();
                }
                else
                {
                    StatusText = $"Host peers={_manager?.ConnectedPeersCount ?? 0}";
                }
            };

            l.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                // P1 仅日志；P2 进 StateSync 解包
                var len = reader.AvailableBytes;
                Log($"Recv {len} bytes from {peer.Address}");
                reader.Recycle();
            };

            l.NetworkErrorEvent += (endPoint, error) =>
            {
                Log($"Net error {endPoint}: {error}");
            };

            l.ConnectionRequestEvent += request =>
            {
                // 简单校验 key；正式应加 token / 房间码
                if (request.Data.GetString() == ConnectKey)
                    request.Accept();
                else
                    request.Reject();
            };
        }

        void Log(string msg)
        {
            Debug.Log("[Net] " + msg);
            OnLog?.Invoke(msg);
        }

        // INetEventListener 空实现（我们用 EventBasedNetListener）
        public void OnPeerConnected(NetPeer peer) { }
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) { }
        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) { }
        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
        public void OnConnectionRequest(ConnectionRequest request) { }
    }
}
