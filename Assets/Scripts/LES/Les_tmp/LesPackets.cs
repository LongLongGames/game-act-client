namespace GameAct.Les
{
    public enum LesPacketType : byte
    {
        /// <summary>LES EntityManager 状态/输入包。</summary>
        EntitySystem = 0,
        /// <summary>自定义可序列化包（Join 等）。</summary>
        Serialized = 1
    }

    /// <summary>客户端连接后发往 Host 的加入请求。</summary>
    public class JoinPacket
    {
        public string UserName { get; set; } = "";
        public ulong GameHash { get; set; }
    }
}
