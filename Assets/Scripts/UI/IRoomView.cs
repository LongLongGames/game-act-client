using System;

namespace GameAct.UI
{
    /// <summary>
    /// 房间等待界面。邀请只在这里。
    /// </summary>
    public interface IRoomView
    {
        event Action OnLeaveClicked;
        event Action OnInviteClicked;
        event Action OnStartClicked;

        void Show();
        void Hide();
        void SetInfo(string roomName, string roomId, string[] memberLines, bool isHost);
        void SetStatus(string text);
    }
}
