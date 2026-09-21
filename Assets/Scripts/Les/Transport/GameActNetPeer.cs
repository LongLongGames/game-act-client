using System;
using LiteEntitySystem.Transport;
using LiteNetLib;
using LiteNetLib.Utils;

namespace GameAct.Les.Transport
{
    /// <summary>
    /// LES AbstractNetPeer ↔ LiteNetLib NetPeer 适配（不依赖 LES 内置 LiteNetPeer 类型名）。
    /// </summary>
    public sealed class GameActNetPeer : AbstractNetPeer
    {
        public readonly NetPeer Peer;

        public GameActNetPeer(NetPeer peer, bool assignToTag)
        {
            Peer = peer ?? throw new ArgumentNullException(nameof(peer));
            if (assignToTag)
                Peer.Tag = this;
        }

        public override void TriggerSend() => Peer.NetManager?.TriggerUpdate();

        public override void SendReliableOrdered(ReadOnlySpan<byte> data)
        {
            var w = new NetDataWriter();
            w.Put(data.ToArray());
            Peer.Send(w, DeliveryMethod.ReliableOrdered);
        }

        public override void SendUnreliable(ReadOnlySpan<byte> data)
        {
            var w = new NetDataWriter();
            w.Put(data.ToArray());
            Peer.Send(w, DeliveryMethod.Unreliable);
        }

        public override int GetMaxUnreliablePacketSize() =>
            Peer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable);

        public override string ToString() => Peer.ToString();
    }
}
