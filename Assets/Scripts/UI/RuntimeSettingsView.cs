using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 设置界面：顶部 Tab（显示 / 音频 / 控制），每 Tab 独立【恢复默认】。
    /// </summary>
    public class RuntimeSettingsView : MonoBehaviour, ISettingsView
    {
        public event Action OnBackClicked;
        public event Action OnDisplayDefaultsClicked;
        public event Action OnAudioDefaultsClicked;
        public event Action OnControlsDefaultsClicked;

        public event Action<bool> OnFullscreenChanged;
        public event Action<int> OnResolutionIndexChanged;
        public event Action<int> OnAntiAliasingIndexChanged;
        public event Action<int> OnVSyncIndexChanged;
        public event Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;

        GameObject _root;
        GameObject _displayPanel;
        GameObject _audioPanel;
        GameObject _controlsPanel;

        Button _tabDisplay;
        Button _tabAudio;
        Button _tabControls;

        Toggle _fullscreenToggle;
        Dropdown _resolutionDd;
        Dropdown _aaDd;
        Dropdown _vsyncDd;

        Slider _bgmSlider;
        Text _bgmValueLabel;
        Slider _sfxSlider;
        Text _sfxValueLabel;

        Text _status;

        Color _tabActive = new Color(0.28f, 0.45f, 0.7f);
        Color _tabInactive = new Color(0.22f, 0.24f, 0.3f);

        public void Build(Transform parent)
        {
            _root = new GameObject("Settings");
            _root.transform.SetParent(parent, false);
            UiUtil.StretchFull(_root.AddComponent<RectTransform>());

            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.06f, 0.09f, 0.98f);

            // 左上返回
            var backBtn = UiUtil.CreateButton(_root.transform, "返回", Vector2.zero,
                new Color(0.35f, 0.38f, 0.45f), new Vector2(100, 40));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = backRt.pivot = new Vector2(0, 1);
            backRt.anchoredPosition = new Vector2(24, -56);
            backBtn.onClick.AddListener(() => OnBackClicked?.Invoke());

            var title = UiUtil.CreateAnchoredLabel(_root.transform, "设  置", 22,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -58), new Vector2(300, 40));
            title.alignment = TextAnchor.MiddleCenter;

            // Tab 栏
            var tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(_root.transform, false);
            var tabRt = tabBar.AddComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0.5f, 1);
            tabRt.anchorMax = new Vector2(0.5f, 1);
            tabRt.pivot = new Vector2(0.5f, 1);
            tabRt.anchoredPosition = new Vector2(0, -110);
            tabRt.sizeDelta = new Vector2(540, 44);

            _tabDisplay = UiUtil.CreateButton(tabBar.transform, "显示", new Vector2(-180, 0), _tabActive, new Vector2(160, 40));
            _tabAudio = UiUtil.CreateButton(tabBar.transform, "音频", new Vector2(0, 0), _tabInactive, new Vector2(160, 40));
            _tabControls = UiUtil.CreateButton(tabBar.transform, "控制", new Vector2(180, 0), _tabInactive, new Vector2(160, 40));
            _tabDisplay.onClick.AddListener(() => SwitchTab(0));
            _tabAudio.onClick.AddListener(() => SwitchTab(1));
            _tabControls.onClick.AddListener(() => SwitchTab(2));

            // 内容面板容器
            var content = new GameObject("Content");
            content.transform.SetParent(_root.transform, false);
            var cRt = content.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = new Vector2(0, -20);
            cRt.sizeDelta = new Vector2(720, 380);
            var cImg = content.AddComponent<Image>();
            cImg.color = new Color(0.1f, 0.11f, 0.14f, 1f);

            BuildDisplayPanel(content.transform);
            BuildAudioPanel(content.transform);
            BuildControlsPanel(content.transform);

            _status = UiUtil.CreateAnchoredLabel(_root.transform, "", 13,
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 24), new Vector2(800, 28));
            _status.alignment = TextAnchor.MiddleCenter;
            _status.color = new Color(0.65f, 0.7f, 0.78f);

            SwitchTab(0);
            _root.SetActive(false);
        }

        void BuildDisplayPanel(Transform parent)
        {
            _displayPanel = new GameObject("DisplayPanel");
            _displayPanel.transform.SetParent(parent, false);
            UiUtil.StretchFull(_displayPanel.AddComponent<RectTransform>());

            float y = 140;
            float step = -52;

            // 全屏
            _fullscreenToggle = UiUtil.CreateToggle(_displayPanel.transform, "全屏", new Vector2(-80, y), Screen.fullScreen, new Vector2(280, 36));
            _fullscreenToggle.onValueChanged.AddListener(v => OnFullscreenChanged?.Invoke(v));
            y += step;

            // 分辨率
            var resLabel = UiUtil.CreateLabel(_displayPanel.transform, "分辨率", 15, new Vector2(-200, y));
            resLabel.alignment = TextAnchor.MiddleLeft;
            var resLr = resLabel.GetComponent<RectTransform>();
            if (resLr != null)
            {
                resLr.anchorMin = resLr.anchorMax = resLr.pivot = new Vector2(0.5f, 0.5f);
                resLr.anchoredPosition = new Vector2(-220, y);
                resLr.sizeDelta = new Vector2(120, 32);
            }
            var resOptions = new[] { "1920×1080", "1600×900", "1280×720", "1280×800", "1024×768" };
            _resolutionDd = UiUtil.CreateDropdown(_displayPanel.transform, new Vector2(40, y), new Vector2(280, 36), resOptions, 0);
            _resolutionDd.onValueChanged.AddListener(i => OnResolutionIndexChanged?.Invoke(i));
            y += step;

            // 抗锯齿
            var aaLabel = UiUtil.CreateLabel(_displayPanel.transform, "抗锯齿", 15, new Vector2(-200, y));
            aaLabel.alignment = TextAnchor.MiddleLeft;
            var aaLr = aaLabel.GetComponent<RectTransform>();
            if (aaLr != null)
            {
                aaLr.anchorMin = aaLr.anchorMax = aaLr.pivot = new Vector2(0.5f, 0.5f);
                aaLr.anchoredPosition = new Vector2(-220, y);
                aaLr.sizeDelta = new Vector2(120, 32);
            }
            var aaOptions = new[] { "关闭", "2x", "4x", "8x" };
            _aaDd = UiUtil.CreateDropdown(_displayPanel.transform, new Vector2(40, y), new Vector2(280, 36), aaOptions, 2);
            _aaDd.onValueChanged.AddListener(i => OnAntiAliasingIndexChanged?.Invoke(i));
            y += step;

            // 垂直同步
            var vsLabel = UiUtil.CreateLabel(_displayPanel.transform, "垂直同步", 15, new Vector2(-200, y));
            vsLabel.alignment = TextAnchor.MiddleLeft;
            var vsLr = vsLabel.GetComponent<RectTransform>();
            if (vsLr != null)
            {
                vsLr.anchorMin = vsLr.anchorMax = vsLr.pivot = new Vector2(0.5f, 0.5f);
                vsLr.anchoredPosition = new Vector2(-220, y);
                vsLr.sizeDelta = new Vector2(120, 32);
            }
            var vsOptions = new[] { "关闭", "开启" };
            _vsyncDd = UiUtil.CreateDropdown(_displayPanel.transform, new Vector2(40, y), new Vector2(280, 36), vsOptions, QualitySettings.vSyncCount > 0 ? 1 : 0);
            _vsyncDd.onValueChanged.AddListener(i => OnVSyncIndexChanged?.Invoke(i));
            y += step - 10;

            var defBtn = UiUtil.CreateButton(_displayPanel.transform, "恢复默认", new Vector2(0, -150),
                new Color(0.45f, 0.4f, 0.35f), new Vector2(160, 40));
            defBtn.onClick.AddListener(() => OnDisplayDefaultsClicked?.Invoke());
        }

        void BuildAudioPanel(Transform parent)
        {
            _audioPanel = new GameObject("AudioPanel");
            _audioPanel.transform.SetParent(parent, false);
            UiUtil.StretchFull(_audioPanel.AddComponent<RectTransform>());

            float y = 100;

            var bgmLabel = UiUtil.CreateLabel(_audioPanel.transform, "BGM", 16, new Vector2(-220, y));
            bgmLabel.alignment = TextAnchor.MiddleLeft;
            var blr = bgmLabel.GetComponent<RectTransform>();
            if (blr != null) { blr.anchoredPosition = new Vector2(-240, y); blr.sizeDelta = new Vector2(80, 32); }

            _bgmSlider = UiUtil.CreateSlider(_audioPanel.transform, new Vector2(20, y), new Vector2(320, 28), 0, 100, 80);
            _bgmValueLabel = UiUtil.CreateLabel(_audioPanel.transform, "80", 15, new Vector2(220, y));
            _bgmValueLabel.alignment = TextAnchor.MiddleLeft;
            var bvr = _bgmValueLabel.GetComponent<RectTransform>();
            if (bvr != null) { bvr.anchoredPosition = new Vector2(220, y); bvr.sizeDelta = new Vector2(60, 32); }
            _bgmSlider.onValueChanged.AddListener(v =>
            {
                if (_bgmValueLabel != null) _bgmValueLabel.text = ((int)v).ToString();
                OnBgmVolumeChanged?.Invoke(v);
            });

            y -= 70;

            var sfxLabel = UiUtil.CreateLabel(_audioPanel.transform, "SFX", 16, new Vector2(-220, y));
            sfxLabel.alignment = TextAnchor.MiddleLeft;
            var slr = sfxLabel.GetComponent<RectTransform>();
            if (slr != null) { slr.anchoredPosition = new Vector2(-240, y); slr.sizeDelta = new Vector2(80, 32); }

            _sfxSlider = UiUtil.CreateSlider(_audioPanel.transform, new Vector2(20, y), new Vector2(320, 28), 0, 100, 100);
            _sfxValueLabel = UiUtil.CreateLabel(_audioPanel.transform, "100", 15, new Vector2(220, y));
            _sfxValueLabel.alignment = TextAnchor.MiddleLeft;
            var svr = _sfxValueLabel.GetComponent<RectTransform>();
            if (svr != null) { svr.anchoredPosition = new Vector2(220, y); svr.sizeDelta = new Vector2(60, 32); }
            _sfxSlider.onValueChanged.AddListener(v =>
            {
                if (_sfxValueLabel != null) _sfxValueLabel.text = ((int)v).ToString();
                OnSfxVolumeChanged?.Invoke(v);
            });

            var defBtn = UiUtil.CreateButton(_audioPanel.transform, "恢复默认", new Vector2(0, -150),
                new Color(0.45f, 0.4f, 0.35f), new Vector2(160, 40));
            defBtn.onClick.AddListener(() => OnAudioDefaultsClicked?.Invoke());
        }

        void BuildControlsPanel(Transform parent)
        {
            _controlsPanel = new GameObject("ControlsPanel");
            _controlsPanel.transform.SetParent(parent, false);
            UiUtil.StretchFull(_controlsPanel.AddComponent<RectTransform>());

            var hint = UiUtil.CreateLabel(_controlsPanel.transform, "键位绑定（占位）", 18, new Vector2(0, 80));
            hint.color = new Color(0.7f, 0.75f, 0.8f);

            var lines = new[]
            {
                "移动  W / A / S / D",
                "跳跃  Space",
                "攻击  鼠标左键",
                "技能1  Q    技能2  E",
                "交互  F    地图  M",
                "暂停  Esc"
            };
            float y = 30;
            foreach (var line in lines)
            {
                var t = UiUtil.CreateLabel(_controlsPanel.transform, line, 14, new Vector2(0, y));
                t.color = new Color(0.6f, 0.65f, 0.72f);
                y -= 28;
            }

            var defBtn = UiUtil.CreateButton(_controlsPanel.transform, "恢复默认", new Vector2(0, -150),
                new Color(0.45f, 0.4f, 0.35f), new Vector2(160, 40));
            defBtn.onClick.AddListener(() => OnControlsDefaultsClicked?.Invoke());
        }

        void SwitchTab(int index)
        {
            if (_displayPanel != null) _displayPanel.SetActive(index == 0);
            if (_audioPanel != null) _audioPanel.SetActive(index == 1);
            if (_controlsPanel != null) _controlsPanel.SetActive(index == 2);

            SetTabColor(_tabDisplay, index == 0);
            SetTabColor(_tabAudio, index == 1);
            SetTabColor(_tabControls, index == 2);
        }

        void SetTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.targetGraphic as Image;
            if (img != null) img.color = active ? _tabActive : _tabInactive;
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void SetDisplay(bool fullscreen, int resolutionIndex, string[] resolutions, int aaIndex, string[] aaOptions, int vsyncIndex)
        {
            if (_fullscreenToggle != null) _fullscreenToggle.isOn = fullscreen;
            if (_resolutionDd != null)
            {
                if (resolutions != null && resolutions.Length > 0)
                {
                    _resolutionDd.ClearOptions();
                    _resolutionDd.AddOptions(new System.Collections.Generic.List<string>(resolutions));
                }
                _resolutionDd.value = Mathf.Clamp(resolutionIndex, 0, _resolutionDd.options.Count - 1);
                _resolutionDd.RefreshShownValue();
            }
            if (_aaDd != null)
            {
                if (aaOptions != null && aaOptions.Length > 0)
                {
                    _aaDd.ClearOptions();
                    _aaDd.AddOptions(new System.Collections.Generic.List<string>(aaOptions));
                }
                _aaDd.value = Mathf.Clamp(aaIndex, 0, _aaDd.options.Count - 1);
                _aaDd.RefreshShownValue();
            }
            if (_vsyncDd != null)
            {
                _vsyncDd.value = Mathf.Clamp(vsyncIndex, 0, _vsyncDd.options.Count - 1);
                _vsyncDd.RefreshShownValue();
            }
        }

        public void SetAudio(float bgm, float sfx)
        {
            if (_bgmSlider != null)
            {
                _bgmSlider.SetValueWithoutNotify(bgm);
                if (_bgmValueLabel != null) _bgmValueLabel.text = ((int)bgm).ToString();
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.SetValueWithoutNotify(sfx);
                if (_sfxValueLabel != null) _sfxValueLabel.text = ((int)sfx).ToString();
            }
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
        }
    }
}
