using System;
using GameAct.Network;

namespace GameAct.UI
{
    public interface IHomeView
    {
        event Action OnSinglePlayerClicked;
        event Action OnMultiplayerClicked;
        event Action OnAchievementsClicked;
        event Action OnSettingsClicked;
        event Action OnExitClicked;

        event Action OnServerListBackClicked;
        event Action OnRefreshServerListClicked;
        event Action OnCreateRoomClicked;
        event Action OnInviteClicked;
        event Action<string> OnJoinRoomClicked;

        // 房间等待界面
        event Action OnRoomLeaveClicked;
        event Action OnRoomInviteClicked;
        event Action OnRoomStartClicked;

        void Show();
        void Hide();

        void SetVersions(string clientVersion, string resourceVersion);
        void SetUserName(string name);
        void SetStatus(string text);
        void ShowProfile(PlayerProfile profile);

        void ShowServerList(bool show);
        void SetServerListStatus(string text);
        void SetRoomList(RoomListItem[] rooms);

        void ShowRoomWaiting(bool show);
        void SetRoomWaitingInfo(string roomName, string roomId, string[] memberLines, bool isHost);
        void SetRoomWaitingStatus(string text);
    }

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
