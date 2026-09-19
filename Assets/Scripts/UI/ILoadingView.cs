using System;

namespace GameAct.UI
{
    /// <summary>
    /// 加载界面（开始游戏后、场景 Additive 加载期间）。
    /// </summary>
    public interface ILoadingView
    {
        void Show();
        void Hide();
        void SetProgress(float progress01);
        void SetStatus(string text);
    }
}
