using System;
using GameAct.Network;

namespace GameAct.UI
{
    public interface IHomeView
    {
        event Action OnLogoutClicked;
        event Action OnCreateLobbyClicked;
        event Action OnInviteFriendsClicked;
        event Action OnStartHostClicked;
        event Action OnConnectLocalClicked;
        event Action OnDisconnectNetClicked;

        void Show();
        void Hide();
        void SetStatus(string text);
        void ShowProfile(PlayerProfile profile);
        void SetSteamStatus(string text);
        void SetNetStatus(string text);
        void SetLobbyId(ulong lobbyId);
    }
}
