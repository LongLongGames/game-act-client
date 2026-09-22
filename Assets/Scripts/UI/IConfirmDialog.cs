using System;
using Cysharp.Threading.Tasks;

namespace GameAct.UI
{
    /// <summary>
    /// 阻断级确认框：断线、致命错误等必须点确认后才继续。
    /// 不要用 Toast 替代。
    /// </summary>
    public interface IConfirmDialog
    {
        /// <summary>
        /// 显示标题+正文，仅一个确认按钮。关闭后返回。
        /// </summary>
        UniTask ShowAsync(string title, string message, string confirmText = "确定");

        /// <summary>
        /// 双按钮：确认 / 取消。返回 true=点确认。
        /// </summary>
        UniTask<bool> ShowConfirmCancelAsync(
            string title,
            string message,
            string confirmText = "确定",
            string cancelText = "取消");
    }
}
