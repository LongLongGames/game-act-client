using System;

namespace GameAct.UI
{
    /// <summary>
    /// 多人大厅 / 房间列表。不含主菜单、房间等待、邀请。
    /// </summary>
    public interface ILobbyView
    {
        event Action OnBackClicked;
        event Action OnRefreshClicked;
        event Action OnCreateRoomClicked;
        event Action<string> OnJoinRoomClicked;

        void Show();
        void Hide();
        void SetStatus(string text);
        void SetRoomList(RoomListItem[] rooms);
    }
}
