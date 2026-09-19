using System;
using GameAct.Network;

namespace GameAct.UI
{
    /// <summary>
    /// 主菜单（登录后首页）。不含大厅/房间/设置/HUD。
    /// </summary>
    public interface IMainMenuView
    {
        event Action OnSinglePlayerClicked;
        event Action OnMultiplayerClicked;
        event Action OnAchievementsClicked;
        event Action OnSettingsClicked;
        event Action OnExitClicked;

        void Show();
        void Hide();
        void SetVersions(string clientVersion, string resourceVersion);
        void SetUserName(string name);
        void SetStatus(string text);
        void ShowProfile(PlayerProfile profile);
    }
}
