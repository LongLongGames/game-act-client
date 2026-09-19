using System;
using Cysharp.Threading.Tasks;

namespace GameAct.Net
{
    public enum NetRole
    {
        None,
        Host,
        Client
    }

    public enum NetTransportKind
    {
        /// <summary>本机 UDP（开发 / 局域网）。</summary>
        Udp,
        /// <summary>Steam P2P（正式联机）。</summary>
        SteamP2P
    }

    /// <summary>
    /// P1 网络会话：Host 或 Client。后续挂 StateSync。
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

        /// <summary>以 Host 启动（监听 port，默认 9050）。</summary>
        UniTask<bool> StartHostAsync(int port = 9050, NetTransportKind transport = NetTransportKind.Udp);

        /// <summary>连接 Host（地址 或 Steam 后续扩展）。</summary>
        UniTask<bool> ConnectAsync(string address, int port = 9050, NetTransportKind transport = NetTransportKind.Udp);

        void Disconnect();

        /// <summary>发送原始字节（可靠）。P1 占位，P2 换成 StateSync 包。</summary>
        void SendReliable(byte[] data);

        void Poll();
    }
}
