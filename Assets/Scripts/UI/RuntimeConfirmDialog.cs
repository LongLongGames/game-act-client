using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 运行时生成的简易确认框（无需预制体）。
    /// 挂 Boot 持久物体或由 Bootstrap 创建；sortingOrder 压过 HUD。
    /// </summary>
    public class RuntimeConfirmDialog : MonoBehaviour, IConfirmDialog
    {
        Canvas _canvas;
        Text _title;
        Text _body;
        Button _btnOk;
        Button _btnCancel;
        Text _okLabel;
        Text _cancelLabel;
        GameObject _cancelGo;

        UniTaskCompletionSource<bool> _tcs;
        bool _built;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureUi();
            SetVisible(false);
        }

        public async UniTask ShowAsync(string title, string message, string confirmText = "确定")
        {
            EnsureUi();
            _title.text = title ?? "";
            _body.text = message ?? "";
            _okLabel.text = string.IsNullOrEmpty(confirmText) ? "确定" : confirmText;
            _cancelGo.SetActive(false);

            _tcs = new UniTaskCompletionSource<bool>();
            SetVisible(true);
            await _tcs.Task;
            SetVisible(false);
        }

        public async UniTask<bool> ShowConfirmCancelAsync(
            string title,
            string message,
            string confirmText = "确定",
            string cancelText = "取消")
        {
            EnsureUi();
            _title.text = title ?? "";
            _body.text = message ?? "";
            _okLabel.text = string.IsNullOrEmpty(confirmText) ? "确定" : confirmText;
            _cancelLabel.text = string.IsNullOrEmpty(cancelText) ? "取消" : cancelText;
            _cancelGo.SetActive(true);

            _tcs = new UniTaskCompletionSource<bool>();
            SetVisible(true);
            bool ok = await _tcs.Task;
            SetVisible(false);
            return ok;
        }

        void EnsureUi()
        {
            if (_built) return;
            _built = true;

            var root = new GameObject("ConfirmDialogCanvas");
            root.transform.SetParent(transform, false);
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 6000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<GraphicRaycaster>();

            // 全屏遮罩
            var maskGo = new GameObject("Mask");
            maskGo.transform.SetParent(root.transform, false);
            var mask = maskGo.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.65f);
            Stretch(mask.rectTransform);

            // 面板
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(root.transform, false);
            var panel = panelGo.AddComponent<Image>();
            panel.color = new Color(0.12f, 0.12f, 0.14f, 0.98f);
            var pr = panel.rectTransform;
            pr.anchorMin = new Vector2(0.5f, 0.5f);
            pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(520, 280);
            pr.anchoredPosition = Vector2.zero;

            _title = MakeText(panelGo.transform, "Title", 26, FontStyle.Bold,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -36), new Vector2(480, 40));

            _body = MakeText(panelGo.transform, "Body", 20, FontStyle.Normal,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 10), new Vector2(460, 100));
            _body.alignment = TextAnchor.MiddleCenter;

            // 确定
            var okGo = MakeButton(panelGo.transform, "Ok", out _btnOk, out _okLabel);
            var okRt = okGo.GetComponent<RectTransform>();
            okRt.anchorMin = new Vector2(0.5f, 0f);
            okRt.anchorMax = new Vector2(0.5f, 0f);
            okRt.sizeDelta = new Vector2(160, 44);
            okRt.anchoredPosition = new Vector2(0, 36);
            _btnOk.onClick.AddListener(() => _tcs?.TrySetResult(true));

            // 取消（双按钮时再显示，默认藏）
            _cancelGo = MakeButton(panelGo.transform, "Cancel", out _btnCancel, out _cancelLabel);
            var cRt = _cancelGo.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0f);
            cRt.anchorMax = new Vector2(0.5f, 0f);
            cRt.sizeDelta = new Vector2(160, 44);
            cRt.anchoredPosition = new Vector2(-100, 36);
            // 有取消时确定右移
            _btnCancel.onClick.AddListener(() => _tcs?.TrySetResult(false));
            _cancelGo.SetActive(false);
        }

        void LayoutSingleOk()
        {
            var okRt = _btnOk.GetComponent<RectTransform>();
            okRt.anchoredPosition = new Vector2(0, 36);
        }

        void LayoutDual()
        {
            var okRt = _btnOk.GetComponent<RectTransform>();
            okRt.anchoredPosition = new Vector2(100, 36);
            var cRt = _cancelGo.GetComponent<RectTransform>();
            cRt.anchoredPosition = new Vector2(-100, 36);
        }

        void SetVisible(bool on)
        {
            if (_canvas == null) return;
            if (on)
            {
                if (_cancelGo != null && _cancelGo.activeSelf)
                    LayoutDual();
                else
                    LayoutSingleOk();
            }
            _canvas.gameObject.SetActive(on);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text MakeText(Transform parent, string name, int size, FontStyle style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return t;
        }

        static GameObject MakeButton(Transform parent, string name, out Button btn, out Text label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.85f, 1f);
            btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            label = textGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            Stretch(label.rectTransform);
            return go;
        }
    }
}
