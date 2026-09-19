using System;

namespace GameAct.UI
{
    public interface ILoginView
    {
        /// <summary>开发登录：官方用户名密码。</summary>
        event Action<string, string> OnDevLoginSubmitted;

        /// <summary>正式：Steam 一键进入。</summary>
        event Action OnSteamEnterClicked;

        void Show();
        void Hide();
        void SetStatus(string text);
        void SetInteractable(bool interactable);

        /// <summary>
        /// 根据 Steam 是否可用切换主按钮。
        /// steamReady=true：主按钮「进入游戏」，开发登录折叠显示。
        /// </summary>
        void SetSteamMode(bool steamReady, string steamPersona);
    }
}
