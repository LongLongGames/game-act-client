using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 正式：Steam 一键进入；开发：官方账号密码（改名保留联调）。
    /// </summary>
    public class RuntimeLoginView : MonoBehaviour, ILoginView
    {
        public event Action<string, string> OnDevLoginSubmitted;
        public event Action OnSteamEnterClicked;

        GameObject _root;
        GameObject _steamBlock;
        GameObject _devBlock;
        InputField _user;
        InputField _pass;
        Text _status;
        Text _title;
        Text _steamHint;
        Button _steamBtn;
        Button _devBtn;

        public void Build(Transform canvasRoot)
        {
            _root = new GameObject("LoginPanel");
            _root.transform.SetParent(canvasRoot, false);
            var rt = _root.AddComponent<RectTransform>();
            StretchFull(rt);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

            var panel = CreatePanel(_root.transform, new Vector2(460, 420));
            var panelT = panel.transform;

            _title = CreateLabel(panelT, "game-act", 22, new Vector2(0, 170));

            // —— 正式：Steam ——
            _steamBlock = new GameObject("SteamBlock");
            _steamBlock.transform.SetParent(panelT, false);
            var srt = _steamBlock.AddComponent<RectTransform>();
            srt.sizeDelta = new Vector2(420, 120);
            srt.anchoredPosition = new Vector2(0, 60);

            _steamHint = CreateLabel(_steamBlock.transform, "Steam 就绪", 14, new Vector2(0, 28));
            _steamHint.color = new Color(0.7f, 0.9f, 1f);

            _steamBtn = CreateButton(_steamBlock.transform, "进入游戏", new Vector2(0, -20), new Color(0.2f, 0.55f, 0.95f));
            _steamBtn.onClick.AddListener(() => OnSteamEnterClicked?.Invoke());

            // —— 开发：官方账号 ——
            _devBlock = new GameObject("DevBlock");
            _devBlock.transform.SetParent(panelT, false);
            var drt = _devBlock.AddComponent<RectTransform>();
            drt.sizeDelta = new Vector2(420, 200);
            drt.anchoredPosition = new Vector2(0, -90);

            CreateLabel(_devBlock.transform, "开发登录（官方账号 · 仅联调）", 13, new Vector2(0, 70))
                .color = new Color(1f, 0.75f, 0.4f);

            _user = CreateInput(_devBlock.transform, "用户名", new Vector2(0, 30));
            _pass = CreateInput(_devBlock.transform, "密码", new Vector2(0, -20));
            _pass.contentType = InputField.ContentType.Password;
            _user.text = "tester1";
            _pass.text = "test1234";

            _devBtn = CreateButton(_devBlock.transform, "开发登录", new Vector2(0, -70), new Color(0.45f, 0.45f, 0.5f));
            _devBtn.onClick.AddListener(() =>
                OnDevLoginSubmitted?.Invoke(_user.text?.Trim(), _pass.text));

            _status = CreateLabel(panelT, "", 13, new Vector2(0, -185));
            _status.color = new Color(0.85f, 0.85f, 0.9f);
            var st = _status.GetComponent<RectTransform>();
            if (st != null) st.sizeDelta = new Vector2(420, 40);

            // 默认先显示开发（Steam 未就绪时）；Steam 就绪后由 SetSteamMode 切换
            SetSteamMode(false, null);
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
            if (_steamBtn != null) _steamBtn.interactable = interactable;
            if (_devBtn != null) _devBtn.interactable = interactable;
            if (_user != null) _user.interactable = interactable;
            if (_pass != null) _pass.interactable = interactable;
        }

        public void SetSteamMode(bool steamReady, string steamPersona)
        {
            if (_steamBlock != null) _steamBlock.SetActive(true);
            if (_devBlock != null) _devBlock.SetActive(true);

            if (steamReady)
            {
                if (_title != null) _title.text = "game-act";
                if (_steamHint != null)
                    _steamHint.text = string.IsNullOrEmpty(steamPersona)
                        ? "Steam 已连接 · 点击进入"
                        : $"以 {steamPersona} 进入";
                if (_steamBtn != null) _steamBtn.interactable = true;
            }
            else
            {
                if (_title != null) _title.text = "game-act（Steam 未就绪）";
                if (_steamHint != null)
                    _steamHint.text = "请启动 Steam 客户端，或使用下方开发登录";
                if (_steamBtn != null) _steamBtn.interactable = false;
            }
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
            rt.sizeDelta = new Vector2(400, 36);
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

        static Button CreateButton(Transform parent, string label, Vector2 pos, Color? color = null)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 40);
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color ?? new Color(0.25f, 0.45f, 0.85f, 1f);
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
