using System;

namespace GameAct.UI
{
    /// <summary>
    /// 游戏内 HUD（进 Map 后显示）。背包 / 邮件 / 任务等入口 + 后续小地图区域。
    /// </summary>
    public interface IGameHudView
    {
        event Action OnBagClicked;
        event Action OnMailClicked;
        event Action OnQuestClicked;
        event Action OnSkillClicked;
        event Action OnMapClicked;

        void Show();
        void Hide();
        void SetStatus(string text);
    }
}
