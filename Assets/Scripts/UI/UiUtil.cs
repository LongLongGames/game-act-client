using UnityEngine;
using UnityEngine.UI;

namespace GameAct.UI
{
    /// <summary>
    /// 运行时 UI 通用构建工具。
    /// </summary>
    public static class UiUtil
    {
        public static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Font BuiltinFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        public static Text CreateLabel(Transform parent, string text, int fontSize, Vector2 pos)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700, 40);
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = BuiltinFont();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Text CreateAnchoredLabel(Transform parent, string text, int fontSize,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = BuiltinFont();
            t.text = text;
            t.fontSize = fontSize;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button CreateButton(Transform parent, string label, Vector2 pos, Color color, Vector2 size)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            StretchFull(trt);
            var t = textGo.AddComponent<Text>();
            t.font = BuiltinFont();
            t.text = label;
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return btn;
        }

        public static GameObject CreatePanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.14f, 0.15f, 0.19f, 1f);
            return go;
        }

        public static Toggle CreateToggle(Transform parent, string label, Vector2 pos, bool isOn, Vector2 size)
        {
            var go = new GameObject("Toggle_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.pivot = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(28, 28);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            var check = new GameObject("Checkmark");
            check.transform.SetParent(bg.transform, false);
            var ckRt = check.AddComponent<RectTransform>();
            StretchFull(ckRt);
            ckRt.offsetMin = new Vector2(4, 4);
            ckRt.offsetMax = new Vector2(-4, -4);
            var ckImg = check.AddComponent<Image>();
            ckImg.color = new Color(0.3f, 0.75f, 0.45f, 1f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lr = labelGo.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 0);
            lr.anchorMax = new Vector2(1, 1);
            lr.offsetMin = new Vector2(40, 0);
            lr.offsetMax = Vector2.zero;
            var t = labelGo.AddComponent<Text>();
            t.font = BuiltinFont();
            t.text = label;
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = Color.white;

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = ckImg;
            toggle.isOn = isOn;
            return toggle;
        }

        public static Slider CreateSlider(Transform parent, Vector2 pos, Vector2 size, float min, float max, float value)
        {
            var go = new GameObject("Slider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            StretchFull(bgRt);
            bgRt.offsetMin = new Vector2(0, size.y * 0.3f);
            bgRt.offsetMax = new Vector2(0, -size.y * 0.3f);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.18f, 0.2f, 0.25f, 1f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            StretchFull(faRt);
            faRt.offsetMin = new Vector2(0, size.y * 0.3f);
            faRt.offsetMax = new Vector2(0, -size.y * 0.3f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fRt = fill.AddComponent<RectTransform>();
            StretchFull(fRt);
            var fImg = fill.AddComponent<Image>();
            fImg.color = new Color(0.3f, 0.55f, 0.85f, 1f);

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.AddComponent<RectTransform>();
            StretchFull(haRt);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var hRt = handle.AddComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(20, size.y);
            var hImg = handle.AddComponent<Image>();
            hImg.color = new Color(0.85f, 0.88f, 0.95f, 1f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fRt;
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.value = value;
            return slider;
        }

        public static Dropdown CreateDropdown(Transform parent, Vector2 pos, Vector2 size, string[] options, int value)
        {
            var go = new GameObject("Dropdown");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lr = labelGo.AddComponent<RectTransform>();
            StretchFull(lr);
            lr.offsetMin = new Vector2(12, 2);
            lr.offsetMax = new Vector2(-28, -2);
            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont();
            label.fontSize = 15;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;

            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(go.transform, false);
            var ar = arrowGo.AddComponent<RectTransform>();
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1, 0.5f);
            ar.anchoredPosition = new Vector2(-10, 0);
            ar.sizeDelta = new Vector2(16, 16);
            var at = arrowGo.AddComponent<Text>();
            at.font = BuiltinFont();
            at.text = "▼";
            at.fontSize = 12;
            at.alignment = TextAnchor.MiddleCenter;
            at.color = new Color(0.7f, 0.75f, 0.8f);

            var template = new GameObject("Template");
            template.transform.SetParent(go.transform, false);
            var tr = template.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(0, 0);
            tr.anchorMax = new Vector2(1, 0);
            tr.pivot = new Vector2(0.5f, 1);
            tr.anchoredPosition = Vector2.zero;
            tr.sizeDelta = new Vector2(0, 150);
            template.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 1f);
            var scroll = template.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            template.SetActive(false);

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            var vpRt = viewport.AddComponent<RectTransform>();
            StretchFull(vpRt);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = Color.white;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var cRt = content.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot = new Vector2(0.5f, 1);
            cRt.sizeDelta = new Vector2(0, 28);

            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            var iRt = item.AddComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0, 0.5f);
            iRt.anchorMax = new Vector2(1, 0.5f);
            iRt.sizeDelta = new Vector2(0, 28);
            var itemToggle = item.AddComponent<Toggle>();
            var itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(0.2f, 0.22f, 0.28f, 0.5f);
            itemToggle.targetGraphic = itemBg;

            var itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            var ilr = itemLabel.AddComponent<RectTransform>();
            StretchFull(ilr);
            ilr.offsetMin = new Vector2(12, 0);
            var ilt = itemLabel.AddComponent<Text>();
            ilt.font = BuiltinFont();
            ilt.fontSize = 14;
            ilt.color = Color.white;
            ilt.alignment = TextAnchor.MiddleLeft;

            scroll.viewport = vpRt;
            scroll.content = cRt;

            var dd = go.AddComponent<Dropdown>();
            dd.targetGraphic = img;
            dd.captionText = label;
            dd.itemText = ilt;
            dd.template = tr;
            dd.ClearOptions();
            if (options != null)
            {
                var list = new System.Collections.Generic.List<string>(options);
                dd.AddOptions(list);
            }
            dd.value = Mathf.Clamp(value, 0, dd.options.Count - 1);
            dd.RefreshShownValue();
            return dd;
        }
    }
}
