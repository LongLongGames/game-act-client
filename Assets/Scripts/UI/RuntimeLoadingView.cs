using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 运行时 Loading 界面：全屏遮罩 + 进度条 + 状态文字。
    /// </summary>
    public class RuntimeLoadingView : MonoBehaviour, ILoadingView
    {
        GameObject _root;
        Text _title;
        Text _status;
        Image _barFill;
        RectTransform _barFillRt;
        float _barWidth = 480f;

        public void Build(Transform parent)
        {
            _root = new GameObject("Loading");
            _root.transform.SetParent(parent, false);
            UiUtil.StretchFull(_root.AddComponent<RectTransform>());

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.96f);

            _title = UiUtil.CreateLabel(_root.transform, "LOADING", 32, new Vector2(0, 40));
            _title.fontStyle = FontStyle.Bold;

            // 进度条背景
            var barBgGo = new GameObject("BarBg");
            barBgGo.transform.SetParent(_root.transform, false);
            var barBgRt = barBgGo.AddComponent<RectTransform>();
            barBgRt.sizeDelta = new Vector2(_barWidth, 16);
            barBgRt.anchoredPosition = new Vector2(0, -20);
            var barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.color = new Color(0.15f, 0.17f, 0.22f, 1f);

            // 进度条填充（左对齐缩放）
            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(barBgGo.transform, false);
            _barFillRt = fillGo.AddComponent<RectTransform>();
            _barFillRt.anchorMin = new Vector2(0, 0);
            _barFillRt.anchorMax = new Vector2(0, 1);
            _barFillRt.pivot = new Vector2(0, 0.5f);
            _barFillRt.anchoredPosition = Vector2.zero;
            _barFillRt.sizeDelta = new Vector2(0, 0);
            _barFill = fillGo.AddComponent<Image>();
            _barFill.color = new Color(0.25f, 0.55f, 0.95f, 1f);

            _status = UiUtil.CreateLabel(_root.transform, "准备中…", 16, new Vector2(0, -60));
            _status.color = new Color(0.7f, 0.75f, 0.85f);

            _root.SetActive(false);
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
            SetProgress(0f);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public void SetProgress(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);
            if (_barFillRt != null)
                _barFillRt.sizeDelta = new Vector2(_barWidth * progress01, 0);
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
        }
    }
}
