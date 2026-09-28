using System.Collections.Generic;
using UnityEngine;

namespace GameAct.Audio
{
    /// <summary>
    /// 运行时音频管理：BGM 单轨淡入淡出 + SFX 对象池。
    /// DontDestroyOnLoad，与设置页 BGM/SFX 滑条对接。
    /// </summary>
    public class AudioManager : MonoBehaviour, IAudioManager
    {
        const string PrefBgm = "set_bgm";
        const string PrefSfx = "set_sfx";
        const string PrefMaster = "set_master";
        const int DefaultSfxPoolSize = 12;

        static AudioManager _instance;
        public static IAudioManager Instance => _instance;

        [SerializeField] int _sfxPoolSize = DefaultSfxPoolSize;

        AudioSource _bgmSource;
        AudioSource _bgmSourceFade; // 交叉淡入备用轨
        readonly List<AudioSource> _sfxPool = new List<AudioSource>();
        int _sfxCursor;

        float _bgmVolume = 0.8f;   // 0~1
        float _sfxVolume = 1f;     // 0~1
        float _masterVolume = 1f;  // 0~1

        float _bgmFadeTarget;
        float _bgmFadeSpeed;
        bool _bgmFading;
        AudioClip _pendingBgm;

        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                ApplyBgmVolume();
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set => _sfxVolume = Mathf.Clamp01(value);
        }

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                ApplyBgmVolume();
            }
        }

        /// <summary>
        /// 确保场景中存在唯一 AudioManager（DontDestroyOnLoad）。
        /// 在 Bootstrap 中调用一次即可。
        /// </summary>
        public static AudioManager Ensure()
        {
            if (_instance != null) return _instance;

            var existing = FindFirstObjectByType<AudioManager>();
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }

            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioManager>();
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildSources();
            LoadPrefs();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void BuildSources()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;

            _bgmSourceFade = gameObject.AddComponent<AudioSource>();
            _bgmSourceFade.playOnAwake = false;
            _bgmSourceFade.loop = true;
            _bgmSourceFade.spatialBlend = 0f;
            _bgmSourceFade.volume = 0f;

            var poolRoot = new GameObject("SfxPool");
            poolRoot.transform.SetParent(transform, false);
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                var src = poolRoot.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                _sfxPool.Add(src);
            }
        }

        void Update()
        {
            if (!_bgmFading) return;

            float dt = Time.unscaledDeltaTime;
            float cur = _bgmSource.volume;
            float next = Mathf.MoveTowards(cur, _bgmFadeTarget, _bgmFadeSpeed * dt);
            _bgmSource.volume = next;

            if (Mathf.Approximately(next, _bgmFadeTarget))
            {
                _bgmFading = false;
                if (_bgmFadeTarget <= 0.001f && _pendingBgm != null)
                {
                    _bgmSource.clip = _pendingBgm;
                    _pendingBgm = null;
                    _bgmSource.Play();
                    StartBgmFade(_bgmVolume * _masterVolume, 0.4f);
                }
                else if (_bgmFadeTarget <= 0.001f)
                {
                    _bgmSource.Stop();
                    _bgmSource.clip = null;
                }
            }
        }

        void StartBgmFade(float target, float seconds)
        {
            _bgmFadeTarget = Mathf.Clamp01(target);
            _bgmFadeSpeed = seconds > 0.01f
                ? Mathf.Abs(_bgmFadeTarget - _bgmSource.volume) / seconds
                : 999f;
            _bgmFading = true;
        }

        void ApplyBgmVolume()
        {
            if (_bgmSource == null) return;
            if (!_bgmFading)
                _bgmSource.volume = _bgmVolume * _masterVolume;
            else
                _bgmFadeTarget = _bgmVolume * _masterVolume;
        }

        // ─── BGM ────────────────────────────────────────────

        public void PlayBgm(AudioClip clip, float fadeSeconds = 0.5f)
        {
            if (clip == null) return;

            if (_bgmSource.clip == clip && _bgmSource.isPlaying)
                return;

            if (_bgmSource.isPlaying && _bgmSource.clip != null)
            {
                _pendingBgm = clip;
                StartBgmFade(0f, fadeSeconds * 0.5f);
            }
            else
            {
                _bgmSource.clip = clip;
                _bgmSource.volume = 0f;
                _bgmSource.Play();
                StartBgmFade(_bgmVolume * _masterVolume, fadeSeconds);
            }
        }

        public void PlayBgm(string resourceName, float fadeSeconds = 0.5f)
        {
            if (string.IsNullOrEmpty(resourceName)) return;
            var clip = Resources.Load<AudioClip>($"Audio/Bgm/{resourceName}");
            if (clip == null)
            {
                Debug.LogWarning($"[Audio] BGM not found: Audio/Bgm/{resourceName}");
                return;
            }
            PlayBgm(clip, fadeSeconds);
        }

        public void StopBgm(float fadeSeconds = 0.3f)
        {
            _pendingBgm = null;
            if (!_bgmSource.isPlaying)
            {
                _bgmSource.clip = null;
                return;
            }
            StartBgmFade(0f, fadeSeconds);
        }

        public void PauseBgm()
        {
            if (_bgmSource.isPlaying)
                _bgmSource.Pause();
        }

        public void ResumeBgm()
        {
            if (_bgmSource.clip != null && !_bgmSource.isPlaying)
                _bgmSource.UnPause();
        }

        // ─── SFX ────────────────────────────────────────────

        AudioSource NextSfxSource()
        {
            // 简单轮询；若当前还在播则找下一个空闲
            for (int i = 0; i < _sfxPool.Count; i++)
            {
                int idx = (_sfxCursor + i) % _sfxPool.Count;
                var s = _sfxPool[idx];
                if (!s.isPlaying)
                {
                    _sfxCursor = (idx + 1) % _sfxPool.Count;
                    return s;
                }
            }
            // 全忙则抢占最旧
            var forced = _sfxPool[_sfxCursor];
            _sfxCursor = (_sfxCursor + 1) % _sfxPool.Count;
            forced.Stop();
            return forced;
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;
            var src = NextSfxSource();
            src.spatialBlend = 0f;
            src.volume = _sfxVolume * _masterVolume * Mathf.Clamp01(volumeScale);
            src.pitch = 1f;
            src.clip = clip;
            src.Play();
        }

        public void PlaySfx(string resourceName, float volumeScale = 1f)
        {
            if (string.IsNullOrEmpty(resourceName)) return;
            var clip = Resources.Load<AudioClip>($"Audio/Sfx/{resourceName}");
            if (clip == null)
            {
                Debug.LogWarning($"[Audio] SFX not found: Audio/Sfx/{resourceName}");
                return;
            }
            PlaySfx(clip, volumeScale);
        }

        public void PlaySfx3D(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            if (clip == null) return;
            var src = NextSfxSource();
            src.spatialBlend = 1f;
            src.transform.position = position;
            src.volume = _sfxVolume * _masterVolume * Mathf.Clamp01(volumeScale);
            src.pitch = 1f;
            src.clip = clip;
            src.Play();
        }

        public void StopAllSfx()
        {
            foreach (var s in _sfxPool)
            {
                if (s != null && s.isPlaying)
                    s.Stop();
            }
        }

        // ─── Prefs（与现有 set_bgm / set_sfx 兼容，单位 0~100）──

        public void LoadPrefs()
        {
            // 现有设置存的是 0~100
            float bgm100 = PlayerPrefs.GetFloat(PrefBgm, 80f);
            float sfx100 = PlayerPrefs.GetFloat(PrefSfx, 100f);
            float master100 = PlayerPrefs.GetFloat(PrefMaster, 100f);
            _bgmVolume = Mathf.Clamp01(bgm100 / 100f);
            _sfxVolume = Mathf.Clamp01(sfx100 / 100f);
            _masterVolume = Mathf.Clamp01(master100 / 100f);
            ApplyBgmVolume();
            // 不再用全局 AudioListener.volume 混在一起
            AudioListener.volume = 1f;
        }

        public void SavePrefs()
        {
            PlayerPrefs.SetFloat(PrefBgm, _bgmVolume * 100f);
            PlayerPrefs.SetFloat(PrefSfx, _sfxVolume * 100f);
            PlayerPrefs.SetFloat(PrefMaster, _masterVolume * 100f);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 供设置页直接调用：传入 0~100 的滑条值。
        /// </summary>
        public void SetBgmVolumeFromUi(float value0to100)
        {
            BgmVolume = value0to100 / 100f;
            SavePrefs();
        }

        public void SetSfxVolumeFromUi(float value0to100)
        {
            SfxVolume = value0to100 / 100f;
            SavePrefs();
        }
    }
}
