using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameAct.AppFlow
{
    public interface IAppFlow
    {
        AppState State { get; }
        UniTask StartAsync(CancellationToken ct = default);

        /// <summary>
        /// 局内安全离开：停玩法、断网、卸关卡，回到房间（若仍在 Lobby）或大厅。
        /// 供 ESC 暂停菜单使用，避免直接点 Editor Stop 触发 Steam Native 崩溃。
        /// </summary>
        UniTask RequestLeaveGameplayAsync();

        /// <summary>
        /// 局内安全退出整个应用（Editor 下结束 Play）。
        /// </summary>
        UniTask RequestQuitFromGameplayAsync();
    }
}
