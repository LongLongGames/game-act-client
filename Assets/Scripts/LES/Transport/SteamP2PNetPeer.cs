using System;
using LiteEntitySystem.Transport;
using Steamworks;

namespace GameAct.Les.Transport
{
    /// <summary>
    /// LES AbstractNetPeer ↔ Steam Networking Sockets 适配。
    /// 与 GameActNetPeer（LiteNet）同级，应用层只认 AbstractNetPeer。
    /// </summary>
    public sealed class SteamP2PNetPeer : AbstractNetPeer
    {
        public readonly HSteamNetConnection Connection;
        public readonly CSteamID RemoteSteamId;

        readonly SteamP2PTransport _transport;
        bool _closed;

        public SteamP2PNetPeer(SteamP2PTransport transport, HSteamNetConnection conn, CSteamID remoteId)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Connection = conn;
            RemoteSteamId = remoteId;
        }

        public bool IsClosed => _closed;

        public override void TriggerSend()
        {
            // Steam 侧无显式 flush 需求；保留空实现以兼容 LES 调用。
        }

        public override void SendReliableOrdered(ReadOnlySpan<byte> data)
        {
            if (_closed || data.IsEmpty) return;
            _transport.Send(Connection, data, reliable: true);
        }

        public override void SendUnreliable(ReadOnlySpan<byte> data)
        {
            if (_closed || data.IsEmpty) return;
            _transport.Send(Connection, data, reliable: false);
        }

        public override int GetMaxUnreliablePacketSize()
        {
            // Steam Networking Sockets 默认 MTU 约 1200；留余量给包头。
            return 1100;
        }

        public void MarkClosed() => _closed = true;

        public override string ToString() =>
            $"SteamP2P({RemoteSteamId.m_SteamID}, conn={Connection.m_HSteamNetConnection})";
    }
}
