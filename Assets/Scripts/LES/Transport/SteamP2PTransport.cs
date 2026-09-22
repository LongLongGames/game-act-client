using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace GameAct.Les.Transport
{
    /// <summary>
    /// Steam Networking Sockets P2P 传输后端。
    /// Host：CreateListenSocketP2P；Client：ConnectP2P(Host SteamID)。
    /// 与 LiteNet UDP 并存，由 LesNetworkHub 按 NetTransportKind 选择。
    /// </summary>
    public sealed class SteamP2PTransport : IDisposable
    {
        public const int DefaultVirtualPort = 0;
        const int MaxMessagesPerPoll = 32;

        public bool IsListening { get; private set; }
        public bool IsConnected { get; private set; }
        public int ConnectionCount => _peers.Count;
        public string StatusText { get; private set; } = "Idle";

        public event Action<SteamP2PNetPeer> OnPeerConnected;
        public event Action<SteamP2PNetPeer, string> OnPeerDisconnected;
        public event Action<SteamP2PNetPeer, byte[]> OnDataReceived;
        public event Action<string> OnLog;

        HSteamListenSocket _listenSocket = HSteamListenSocket.Invalid;
        HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;
        HSteamNetConnection _clientConn = HSteamNetConnection.Invalid;

        readonly Dictionary<HSteamNetConnection, SteamP2PNetPeer> _peers =
            new Dictionary<HSteamNetConnection, SteamP2PNetPeer>();

        Callback<SteamNetConnectionStatusChangedCallback_t> _cbStatus;
        bool _relayInitTried;
        bool _disposed;
        int _virtualPort = DefaultVirtualPort;

        // 可复用接收缓冲
        readonly IntPtr[] _msgPtrs = new IntPtr[MaxMessagesPerPoll];

        public bool StartHost(int virtualPort = DefaultVirtualPort)
        {
            if (_disposed) return false;
            ShutdownInternal();

            if (!EnsureSteamReady())
                return false;

            EnsureRelay();
            _virtualPort = virtualPort;

            _pollGroup = SteamNetworkingSockets.CreatePollGroup();
            if (_pollGroup == HSteamNetPollGroup.Invalid)
            {
                StatusText = "CreatePollGroup 失败";
                Log(StatusText);
                return false;
            }

            _cbStatus = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);

            _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);
            if (_listenSocket == HSteamListenSocket.Invalid)
            {
                StatusText = "CreateListenSocketP2P 失败";
                Log(StatusText);
                CleanupHandles();
                return false;
            }

            IsListening = true;
            IsConnected = true;
            StatusText = $"SteamP2P Host listening vport={virtualPort} steamId={SteamUser.GetSteamID().m_SteamID}";
            Log(StatusText);
            return true;
        }

        public bool Connect(ulong hostSteamId, int virtualPort = DefaultVirtualPort)
        {
            if (_disposed) return false;
            ShutdownInternal();

            if (hostSteamId == 0)
            {
                StatusText = "Host SteamID 为空";
                return false;
            }

            if (!EnsureSteamReady())
                return false;

            EnsureRelay();
            _virtualPort = virtualPort;

            _pollGroup = SteamNetworkingSockets.CreatePollGroup();
            if (_pollGroup == HSteamNetPollGroup.Invalid)
            {
                StatusText = "CreatePollGroup 失败";
                Log(StatusText);
                return false;
            }

            _cbStatus = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionStatusChanged);

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(hostSteamId));

            _clientConn = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null);
            if (_clientConn == HSteamNetConnection.Invalid)
            {
                StatusText = $"ConnectP2P 失败 → {hostSteamId}";
                Log(StatusText);
                CleanupHandles();
                return false;
            }

            // 先登记，等 Connected 回调再触发 OnPeerConnected
            var peer = new SteamP2PNetPeer(this, _clientConn, new CSteamID(hostSteamId));
            _peers[_clientConn] = peer;
            SteamNetworkingSockets.SetConnectionPollGroup(_clientConn, _pollGroup);

            IsListening = false;
            IsConnected = false; // 等状态回调
            StatusText = $"SteamP2P Connecting → {hostSteamId}…";
            Log(StatusText);
            return true;
        }

        public void Poll()
        {
            if (_disposed) return;
            // 状态回调由 SteamAPI.RunCallbacks（SteamRunner）驱动；
            // 这里只收消息。
            ReceiveMessages();
        }

        public void Send(HSteamNetConnection conn, ReadOnlySpan<byte> data, bool reliable)
        {
            if (_disposed || conn == HSteamNetConnection.Invalid || data.IsEmpty)
                return;

            // 分配临时非托管缓冲；SendMessageToConnection 会拷贝。
            var handle = GCHandle.Alloc(data.ToArray(), GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                int flags = reliable
                    ? Constants.k_nSteamNetworkingSend_ReliableNoNagle
                    : Constants.k_nSteamNetworkingSend_UnreliableNoNagle;

                var result = SteamNetworkingSockets.SendMessageToConnection(
                    conn, ptr, (uint)data.Length, flags, out long _);

                if (result != EResult.k_EResultOK && result != EResult.k_EResultIgnored)
                    Log($"Send failed conn={conn.m_HSteamNetConnection} result={result}");
            }
            finally
            {
                handle.Free();
            }
        }

        public void DisconnectPeer(HSteamNetConnection conn)
        {
            if (conn == HSteamNetConnection.Invalid) return;
            SteamNetworkingSockets.CloseConnection(conn, 0, "disconnect", false);
            RemovePeer(conn, "local_close");
        }

        public void Shutdown()
        {
            ShutdownInternal();
            StatusText = "Disconnected";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ShutdownInternal();
        }

        // ─── internal ───────────────────────────────────────

        void ShutdownInternal()
        {
            foreach (var kv in _peers)
            {
                try
                {
                    kv.Value.MarkClosed();
                    SteamNetworkingSockets.CloseConnection(kv.Key, 0, "shutdown", false);
                }
                catch { /* ignore */ }
            }
            _peers.Clear();

            if (_listenSocket != HSteamListenSocket.Invalid)
            {
                try { SteamNetworkingSockets.CloseListenSocket(_listenSocket); }
                catch { /* ignore */ }
                _listenSocket = HSteamListenSocket.Invalid;
            }

            if (_clientConn != HSteamNetConnection.Invalid)
            {
                // 已在 peers 关闭；清引用
                _clientConn = HSteamNetConnection.Invalid;
            }

            CleanupHandles();
            IsListening = false;
            IsConnected = false;
        }

        void CleanupHandles()
        {
            if (_pollGroup != HSteamNetPollGroup.Invalid)
            {
                try { SteamNetworkingSockets.DestroyPollGroup(_pollGroup); }
                catch { /* ignore */ }
                _pollGroup = HSteamNetPollGroup.Invalid;
            }

            // Callback 对象由 GC 回收；置空即可
            _cbStatus = null;
        }

        bool EnsureSteamReady()
        {
            try
            {
                if (!SteamAPI.IsSteamRunning())
                {
                    StatusText = "Steam 未运行";
                    Log(StatusText);
                    return false;
                }
                // 探测 SteamNetworkingSockets 是否可用
                var test = SteamNetworkingSockets.CreatePollGroup();
                if (test == HSteamNetPollGroup.Invalid)
                {
                    StatusText = "SteamNetworkingSockets 不可用";
                    Log(StatusText);
                    return false;
                }
                SteamNetworkingSockets.DestroyPollGroup(test);
                return true;
            }
            catch (Exception e)
            {
                StatusText = "Steam Networking 不可用: " + e.Message;
                Log(StatusText);
                return false;
            }
        }

        void EnsureRelay()
        {
            if (_relayInitTried) return;
            _relayInitTried = true;
            try
            {
                SteamNetworkingUtils.InitRelayNetworkAccess();
                Log("InitRelayNetworkAccess OK");
            }
            catch (Exception e)
            {
                Log("InitRelayNetworkAccess: " + e.Message);
            }
        }

        void OnConnectionStatusChanged(SteamNetConnectionStatusChangedCallback_t ev)
        {
            var conn = ev.m_hConn;
            var state = ev.m_info.m_eState;
            var oldState = ev.m_eOldState;

            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    // Host 收到入站：Accept
                    if (_listenSocket != HSteamListenSocket.Invalid &&
                        ev.m_info.m_hListenSocket == _listenSocket)
                    {
                        var accept = SteamNetworkingSockets.AcceptConnection(conn);
                        if (accept != EResult.k_EResultOK)
                        {
                            Log($"AcceptConnection failed: {accept}");
                            SteamNetworkingSockets.CloseConnection(conn, 0, "accept_fail", false);
                        }
                        else
                        {
                            SteamNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
                            Log($"Accepting conn from {ev.m_info.m_identityRemote.GetSteamID().m_SteamID}");
                        }
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                {
                    var remoteId = ev.m_info.m_identityRemote.GetSteamID();
                    if (!_peers.TryGetValue(conn, out var peer))
                    {
                        peer = new SteamP2PNetPeer(this, conn, remoteId);
                        _peers[conn] = peer;
                        SteamNetworkingSockets.SetConnectionPollGroup(conn, _pollGroup);
                    }

                    if (!IsListening)
                    {
                        // Client 侧
                        IsConnected = true;
                        StatusText = $"SteamP2P Connected → {remoteId.m_SteamID}";
                    }
                    else
                    {
                        StatusText = $"SteamP2P Host peers={_peers.Count}";
                    }

                    Log($"Peer connected {peer}");
                    OnPeerConnected?.Invoke(peer);
                    break;
                }

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                {
                    string reason = state.ToString();
                    try
                    {
                        if (!string.IsNullOrEmpty(ev.m_info.m_szEndDebug))
                            reason = ev.m_info.m_szEndDebug;
                    }
                    catch { /* ignore */ }

                    Log($"Peer closed conn={conn.m_HSteamNetConnection} reason={reason} old={oldState}");
                    SteamNetworkingSockets.CloseConnection(conn, 0, null, false);
                    RemovePeer(conn, reason);

                    if (!IsListening && conn == _clientConn)
                    {
                        IsConnected = false;
                        StatusText = "Disconnected: " + reason;
                        _clientConn = HSteamNetConnection.Invalid;
                    }
                    break;
                }
            }
        }

        void RemovePeer(HSteamNetConnection conn, string reason)
        {
            if (!_peers.TryGetValue(conn, out var peer)) return;
            peer.MarkClosed();
            _peers.Remove(conn);
            OnPeerDisconnected?.Invoke(peer, reason);
        }

        void ReceiveMessages()
        {
            if (_pollGroup == HSteamNetPollGroup.Invalid) return;

            int n = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _msgPtrs, MaxMessagesPerPoll);
            for (int i = 0; i < n; i++)
            {
                try
                {
                    var msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_msgPtrs[i]);
                    var conn = msg.m_conn;
                    if (!_peers.TryGetValue(conn, out var peer))
                    {
                        SteamNetworkingMessage_t.Release(_msgPtrs[i]);
                        continue;
                    }

                    if (msg.m_cbSize > 0 && msg.m_pData != IntPtr.Zero)
                    {
                        var buf = new byte[msg.m_cbSize];
                        Marshal.Copy(msg.m_pData, buf, 0, msg.m_cbSize);
                        OnDataReceived?.Invoke(peer, buf);
                    }

                    SteamNetworkingMessage_t.Release(_msgPtrs[i]);
                }
                catch (Exception e)
                {
                    Log("ReceiveMessages: " + e.Message);
                    try { SteamNetworkingMessage_t.Release(_msgPtrs[i]); } catch { /* ignore */ }
                }
            }
        }

        void Log(string msg)
        {
            Debug.Log("[SteamP2P] " + msg);
            OnLog?.Invoke(msg);
        }
    }
}
