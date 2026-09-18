using System;
using GameAct.Network;

namespace GameAct.UI
{
    public interface IHomeView
    {
        event Action OnLogoutClicked;

        void Show();
        void Hide();
        void SetStatus(string text);
        void ShowProfile(PlayerProfile profile);
    }
}
