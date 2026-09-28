using UnityEngine;

namespace GameAct.Audio
{
    /// <summary>
    /// 音频管理接口：BGM / SFX 分轨、音量、播放控制。
    /// </summary>
    public interface IAudioManager
    {
        float BgmVolume { get; set; }
        float SfxVolume { get; set; }
        float MasterVolume { get; set; }

        /// <summary>播放 BGM（循环）。同名已在播则忽略；不同则淡入切换。</summary>
        void PlayBgm(AudioClip clip, float fadeSeconds = 0.5f);

        /// <summary>按 Resources 路径播放 BGM（相对 Resources/Audio/Bgm/）。</summary>
        void PlayBgm(string resourceName, float fadeSeconds = 0.5f);

        void StopBgm(float fadeSeconds = 0.3f);
        void PauseBgm();
        void ResumeBgm();

        /// <summary>播放一次性 SFX（2D）。</summary>
        void PlaySfx(AudioClip clip, float volumeScale = 1f);

        /// <summary>按 Resources 路径播放 SFX（相对 Resources/Audio/Sfx/）。</summary>
        void PlaySfx(string resourceName, float volumeScale = 1f);

        /// <summary>在世界坐标播放 3D SFX。</summary>
        void PlaySfx3D(AudioClip clip, Vector3 position, float volumeScale = 1f);

        void StopAllSfx();

        /// <summary>从 PlayerPrefs 加载并应用音量。</summary>
        void LoadPrefs();

        /// <summary>将当前音量写入 PlayerPrefs。</summary>
        void SavePrefs();
    }
}
