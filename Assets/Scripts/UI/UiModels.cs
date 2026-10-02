using System;

namespace GameAct.UI
{
    /// <summary>
    /// 大厅房间列表行数据（与具体 View 解耦）。
    /// </summary>
    [Serializable]
    public class RoomListItem
    {
        public string id;
        public string title;
        public string subtitle;
        public int players;
        public int maxPlayers;
        /// <summary>估算 RTT（ms）。&lt;0 表示未知。</summary>
        public int pingMs = -1;
    }
}
