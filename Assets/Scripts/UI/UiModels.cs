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
    }
}
