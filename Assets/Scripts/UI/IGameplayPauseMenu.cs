using System;
using Cysharp.Threading.Tasks;

namespace GameAct.UI
{
    /// <summary>
    /// 局内 ESC 暂停菜单。由 Gameplay 阶段启用，提供继续 / 回房间或大厅 / 退出游戏。
    /// </summary>
    public interface IGameplayPauseMenu
    {
        bool IsOpen { get; }

        /// <summary>打开或关闭暂停菜单（ESC 切换）。</summary>
        void Toggle();

        void Open();
        void Close();

        /// <summary>销毁运行时 UI 与监听。</summary>
        void Dispose();
    }
}
