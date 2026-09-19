using System;

namespace GameAct.UI
{
    /// <summary>
    /// 设置：显示 / 音频 / 控制 三个 Tab，各有独立【恢复默认】。
    /// </summary>
    public interface ISettingsView
    {
        event Action OnBackClicked;
        event Action OnDisplayDefaultsClicked;
        event Action OnAudioDefaultsClicked;
        event Action OnControlsDefaultsClicked;

        event Action<bool> OnFullscreenChanged;
        event Action<int> OnResolutionIndexChanged;
        event Action<int> OnAntiAliasingIndexChanged;
        event Action<int> OnVSyncIndexChanged;
        event Action<float> OnBgmVolumeChanged;
        event Action<float> OnSfxVolumeChanged;

        void Show();
        void Hide();
        void SetDisplay(bool fullscreen, int resolutionIndex, string[] resolutions, int aaIndex, string[] aaOptions, int vsyncIndex);
        void SetAudio(float bgm, float sfx);
        void SetStatus(string text);
    }
}
