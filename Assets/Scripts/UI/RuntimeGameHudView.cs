using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 游戏内 HUD 占位：
    /// - 右上角一排入口：背包 / 邮件 / 任务 / 技能 / 地图
    /// - 左上角预留小地图区域（占位框）
    /// - 底部状态条
    /// </summary>
    public class RuntimeGameHudView : MonoBehaviour, IGameHudView
    {
        public event Action OnBagClicked;
        public event Action OnMailClicked;
        public event Action OnQuestClicked;
        public event Action OnSkillClicked;
        public event Action OnMapClicked;

        GameObject _root;
        Text _status;
        Text _minimapLabel;

        public void Build(Transform parent)
        {
            _root = new GameObject("GameHUD");
            _root.transform.SetParent(parent, false);
            UiUtil.StretchFull(_root.AddComponent<RectTransform>());
            // 不铺全屏底色，保持透出 3D 场景

            BuildTopRightBar(_root.transform);
            BuildMinimapPlaceholder(_root.transform);
            BuildStatusBar(_root.transform);

            _root.SetActive(false);
        }

        void BuildTopRightBar(Transform parent)
        {
            // 右上角横向按钮条
            var bar = new GameObject("TopRightBar");
            bar.transform.SetParent(parent, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-16, -12);
            rt.sizeDelta = new Vector2(520, 48);

            float x = -40;
            float step = -96;
            AddHudButton(bar.transform, "背包", new Vector2(x, 0), () => OnBagClicked?.Invoke(), new Color(0.35f, 0.45f, 0.7f));
            x += step;
            AddHudButton(bar.transform, "邮件", new Vector2(x, 0), () => OnMailClicked?.Invoke(), new Color(0.3f, 0.55f, 0.55f));
            x += step;
            AddHudButton(bar.transform, "任务", new Vector2(x, 0), () => OnQuestClicked?.Invoke(), new Color(0.5f, 0.45f, 0.65f));
            x += step;
            AddHudButton(bar.transform, "技能", new Vector2(x, 0), () => OnSkillClicked?.Invoke(), new Color(0.55f, 0.4f, 0.35f));
            x += step;
            AddHudButton(bar.transform, "地图", new Vector2(x, 0), () => OnMapClicked?.Invoke(), new Color(0.4f, 0.5f, 0.4f));
        }

        void BuildMinimapPlaceholder(Transform parent)
        {
            // 左上角小地图占位框（后续可在此画 RenderTexture / 图标）
            var box = new GameObject("MinimapPlaceholder");
            box.transform.SetParent(parent, false);
            var rt = box.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(16, -12);
            rt.sizeDelta = new Vector2(180, 180);

            var img = box.AddComponent<Image>();
            img.color = new Color(0.08f, 0.1f, 0.14f, 0.75f);

            // 边框感：内层深色
            var inner = new GameObject("Inner");
            inner.transform.SetParent(box.transform, false);
            var irt = inner.AddComponent<RectTransform>();
            UiUtil.StretchFull(irt);
            irt.offsetMin = new Vector2(4, 4);
            irt.offsetMax = new Vector2(-4, -4);
            var iimg = inner.AddComponent<Image>();
            iimg.color = new Color(0.12f, 0.15f, 0.2f, 0.9f);

            _minimapLabel = UiUtil.CreateLabel(inner.transform, "小地图", 14, Vector2.zero);
            _minimapLabel.color = new Color(0.55f, 0.6f, 0.7f);
        }

        void BuildStatusBar(Transform parent)
        {
            _status = UiUtil.CreateAnchoredLabel(parent, "", 14,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 18), new Vector2(720, 28));
            _status.alignment = TextAnchor.MiddleCenter;
            _status.color = new Color(0.85f, 0.88f, 0.92f);
        }

        void AddHudButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var btn = UiUtil.CreateButton(parent, label, pos, color, new Vector2(88, 40));
            // CreateButton 默认居中锚点，右上条里需要相对父节点定位
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
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

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
        }
    }
}
