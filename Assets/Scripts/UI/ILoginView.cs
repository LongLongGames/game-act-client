using System;

namespace GameAct.UI
{
    public interface ILoginView
    {
        event Action<string, string> OnLoginSubmitted;

        void Show();
        void Hide();
        void SetStatus(string text);
        void SetInteractable(bool interactable);
    }
}
