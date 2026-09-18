using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 运行时创建的简易登录 UI（无需 Prefab，方便 P0 联调）。
    /// 优先使用 TextMeshPro，若无则回退到 Legacy Text。
    /// </summary>
    public class RuntimeLoginView : MonoBehaviour, ILoginView
    {
        public event Action<string, string> OnLoginSubmitted;

        GameObject _root;
        InputField _user;
        InputField _pass;
        Text _status;
        Button _btn;

        public void Build(Transform canvasRoot)
        {
            _root = new GameObject("LoginPanel");
            _root.transform.SetParent(canvasRoot, false);
            var rt = _root.AddComponent<RectTransform>();
            StretchFull(rt);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            var panel = CreatePanel(_root.transform, new Vector2(420, 320));
            var panelT = panel.transform;

            CreateLabel(panelT, "game-act 登录 (P0)", 22, new Vector2(0, 110));

            _user = CreateInput(panelT, "用户名", new Vector2(0, 40));
            _pass = CreateInput(panelT, "密码", new Vector2(0, -20));
            _pass.contentType = InputField.ContentType.Password;

            _btn = CreateButton(panelT, "登录", new Vector2(0, -90));
            _btn.onClick.AddListener(() =>
            {
                OnLoginSubmitted?.Invoke(_user.text?.Trim(), _pass.text);
            });

            _status = CreateLabel(panelT, "", 14, new Vector2(0, -140));
            _status.color = new Color(0.85f, 0.85f, 0.9f);

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

        public void SetInteractable(bool interactable)
        {
            if (_btn != null) _btn.interactable = interactable;
            if (_user != null) _user.interactable = interactable;
            if (_pass != null) _pass.interactable = interactable;
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
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.16f, 0.2f, 1f);
            return go;
        }

        static Text CreateLabel(Transform parent, string text, int fontSize, Vector2 pos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380, 40);
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

        static InputField CreateInput(Transform parent, string placeholder, Vector2 pos)
        {
            var go = new GameObject("Input_" + placeholder);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 36);
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.24f, 0.3f, 1f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            StretchFull(trt);
            trt.offsetMin = new Vector2(8, 4);
            trt.offsetMax = new Vector2(-8, -4);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.supportRichText = false;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var prt = phGo.AddComponent<RectTransform>();
            StretchFull(prt);
            prt.offsetMin = new Vector2(8, 4);
            prt.offsetMax = new Vector2(-8, -4);
            var ph = phGo.AddComponent<Text>();
            ph.font = text.font;
            ph.fontSize = 16;
            ph.color = new Color(1, 1, 1, 0.35f);
            ph.text = placeholder;

            var input = go.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = ph;
            return input;
        }

        static Button CreateButton(Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 40);
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.85f, 1f);
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
