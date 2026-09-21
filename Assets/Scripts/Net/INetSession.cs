using System;
using Cysharp.Threading.Tasks;

namespace GameAct.Net
{
    public enum NetRole
    {
        /// <summary>空闲：未 Host / 未 Connect。单机应保持此状态。</summary>
        None,
        Host,
        Client
    }

    public enum NetTransportKind
    {
        /// <summary>本机 UDP（开发 / 局域网）。</summary>
        Udp,
        /// <summary>Steam P2P（正式联机）。</summary>
        SteamP2P,
        /// <summary>进程内 loopback（同进程 Server+LocalClient，不经 socket）。预留。</summary>
        InProcess
    }

    /// <summary>
    /// 网络会话：Host 或 Client。
    /// 单机路径不得 StartHost/Connect；Role 保持 None，NetRunner 不 Poll。
    /// </summary>
    public interface INetSession
    {
        NetRole Role { get; }
        bool IsConnected { get; }
        int PeerCount { get; }
        string StatusText { get; }

        event Action OnConnected;
        event Action OnDisconnected;
        event Action<string> OnLog;

        /// <summary>以 Host 启动（监听 port，默认 9050）。仅多人 Host。</summary>
        UniTask<bool> StartHostAsync(int port = 9050, NetTransportKind transport = NetTransportKind.Udp);

        /// <summary>连接 Host。仅多人 Client。</summary>
        UniTask<bool> ConnectAsync(string address, int port = 9050, NetTransportKind transport = NetTransportKind.Udp);

        void Disconnect();

        /// <summary>发送原始字节（可靠）。P1 占位；Replication 阶段由 LES 接管。</summary>
        void SendReliable(byte[] data);

        void Poll();
    }
}
