using System;
using UnityEngine;
using UnityEngine.UI;
using GameAct.Network;

namespace GameAct.UI
{
    /// <summary>
    /// 主菜单 UI（单人 / 多人 / 成就 / 设置 / 退出）。
    /// </summary>
    public class RuntimeMainMenuView : MonoBehaviour, IMainMenuView
    {
        public event Action OnSinglePlayerClicked;
        public event Action OnMultiplayerClicked;
        public event Action OnAchievementsClicked;
        public event Action OnSettingsClicked;
        public event Action OnExitClicked;

        GameObject _root;
        Text _versionLabel;
        Text _userLabel;
        Text _status;

        public void Build(Transform parent)
        {
            _root = new GameObject("MainMenu");
            _root.transform.SetParent(parent, false);
            UiUtil.StretchFull(_root.AddComponent<RectTransform>());

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.98f);

            BuildTopBar(_root.transform);

            var title = UiUtil.CreateLabel(_root.transform, "GAME ACT", 36, new Vector2(0, 140));
            title.fontStyle = FontStyle.Bold;

            var subtitle = UiUtil.CreateLabel(_root.transform, "主菜单", 16, new Vector2(0, 95));
            subtitle.color = new Color(0.6f, 0.65f, 0.75f);

            float y = 30;
            float step = -58;
            AddMenuButton(_root.transform, "单  人", new Vector2(0, y), () => OnSinglePlayerClicked?.Invoke(), new Color(0.22f, 0.48f, 0.85f));
            y += step;
            AddMenuButton(_root.transform, "多  人", new Vector2(0, y), () => OnMultiplayerClicked?.Invoke(), new Color(0.2f, 0.62f, 0.45f));
            y += step;
            AddMenuButton(_root.transform, "成  就", new Vector2(0, y), () => OnAchievementsClicked?.Invoke(), new Color(0.45f, 0.4f, 0.7f));
            y += step;
            AddMenuButton(_root.transform, "设  置", new Vector2(0, y), () => OnSettingsClicked?.Invoke(), new Color(0.4f, 0.42f, 0.48f));
            y += step;
            AddMenuButton(_root.transform, "退  出", new Vector2(0, y), () => OnExitClicked?.Invoke(), new Color(0.65f, 0.28f, 0.28f));

            _status = UiUtil.CreateAnchoredLabel(_root.transform, "", 14,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 20), new Vector2(900, 32));
            _status.alignment = TextAnchor.MiddleCenter;
            _status.color = new Color(0.8f, 0.82f, 0.88f);

            _root.SetActive(false);
        }

        void BuildTopBar(Transform parent)
        {
            _versionLabel = UiUtil.CreateAnchoredLabel(parent, "客户端 v?  |  资源 ?", 14,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(16, -12), new Vector2(480, 32));
            _versionLabel.alignment = TextAnchor.MiddleLeft;
            _versionLabel.color = new Color(0.65f, 0.7f, 0.78f);

            _userLabel = UiUtil.CreateAnchoredLabel(parent, "未登录", 16,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-16, -12), new Vector2(360, 32));
            _userLabel.alignment = TextAnchor.MiddleRight;
            _userLabel.color = new Color(0.9f, 0.92f, 0.95f);
        }

        void AddMenuButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var btn = UiUtil.CreateButton(parent, label, pos, color, new Vector2(280, 48));
            btn.onClick.AddListener(onClick);
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void SetVersions(string clientVersion, string resourceVersion)
        {
            if (_versionLabel == null) return;
            _versionLabel.text = $"客户端 {clientVersion ?? "?"}  |  资源 {resourceVersion ?? "?"}";
        }

        public void SetUserName(string name)
        {
            if (_userLabel == null) return;
            _userLabel.text = string.IsNullOrEmpty(name) ? "未登录" : name;
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
        }

        public void ShowProfile(PlayerProfile profile)
        {
            if (profile == null)
            {
                SetUserName("游客");
                return;
            }
            var name = !string.IsNullOrEmpty(profile.nickname) ? profile.nickname : profile.id;
            if (profile.level > 0)
                name = $"{name}  Lv.{profile.level}";
            SetUserName(name);
            SetStatus("资料已加载");
        }
    }
}
