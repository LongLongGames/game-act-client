using System;
using UnityEngine;
using UnityEngine.UI;
using GameAct.Network;

namespace GameAct.UI
{
    public class RuntimeHomeView : MonoBehaviour, IHomeView
    {
        public event Action OnLogoutClicked;

        GameObject _root;
        Text _status;
        Text _profile;

        public void Build(Transform canvasRoot)
        {
            _root = new GameObject("HomePanel");
            _root.transform.SetParent(canvasRoot, false);
            var rt = _root.AddComponent<RectTransform>();
            StretchFull(rt);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            var panel = CreatePanel(_root.transform, new Vector2(480, 360));
            var panelT = panel.transform;

            CreateLabel(panelT, "Home / 资料 (P0)", 22, new Vector2(0, 130));

            _profile = CreateLabel(panelT, "", 16, new Vector2(0, 40));
            _profile.alignment = TextAnchor.UpperLeft;

            _status = CreateLabel(panelT, "", 14, new Vector2(0, -80));
            _status.color = new Color(0.85f, 0.85f, 0.9f);

            var btn = CreateButton(panelT, "登出", new Vector2(0, -140));
            btn.onClick.AddListener(() => OnLogoutClicked?.Invoke());

            _root.SetActive(false);
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

        public void ShowProfile(PlayerProfile profile)
        {
            if (_profile == null) return;
            if (profile == null)
            {
                _profile.text = "(无资料)";
                return;
            }
            _profile.text =
                $"nickname : {profile.nickname}\n" +
                $"level    : {profile.level}\n" +
                $"game_id  : {profile.game_id}\n" +
                $"mp_id    : {profile.mp_account_id}\n" +
                $"id       : {profile.id}";
            SetStatus("资料已加载");
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static GameObject CreatePanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.16f, 0.2f, 1f);
            return go;
        }

        static Text CreateLabel(Transform parent, string text, int fontSize, Vector2 pos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420, 120);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null)
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Button CreateButton(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 40);
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.7f, 0.25f, 0.25f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            StretchFull(trt);
            var t = textGo.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null)
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = label;
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return btn;
        }
    }
}
