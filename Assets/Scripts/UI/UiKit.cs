using UnityEngine;
using UnityEngine.UI;

namespace ParryRL
{
    /// <summary>코드로 uGUI를 만들 때 쓰는 공용 헬퍼.</summary>
    public static class UiKit
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public static Image Image(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>fillAmount로 원형 쿨타임을 그리는 이미지 (Filled는 스프라이트가 있어야 동작).</summary>
        public static Image RadialImage(string name, RectTransform parent, Color color)
        {
            var img = Image(name, parent, color);
            img.sprite = GameAssets.Square;
            img.type = UnityEngine.UI.Image.Type.Filled;
            img.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            img.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            img.fillClockwise = false;
            img.fillAmount = 0f;
            return img;
        }

        public static Text Text(string name, RectTransform parent, int size, TextAnchor anchor, bool outline = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.8f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return text;
        }

        /// <summary>
        /// 가로 슬라이더. 키보드 내비게이션은 끈다 (←/→가 캐릭터 이동과 겹치지 않도록).
        /// </summary>
        public static Slider Slider(string name, RectTransform parent, Vector2 size, Color fillColor)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Slider)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.sizeDelta = size;

            var bg = Image("Background", root, new Color(1f, 1f, 1f, 0.15f));
            bg.raycastTarget = true;
            bg.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0f, 8f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(root, false);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-10f, 8f);
            var fill = Image("Fill", fillArea, fillColor);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(root, false);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(5f, 0f);
            handleArea.offsetMax = new Vector2(-5f, 0f);
            var handle = Image("Handle", handleArea, Color.white);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(14f, 0f);
            handle.rectTransform.anchorMin = new Vector2(0f, 0.15f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.85f);

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        public static Button Button(string name, RectTransform parent, string label, int fontSize, Color color)
        {
            var img = Image(name, parent, color);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            var text = Text("Label", img.rectTransform, fontSize, TextAnchor.MiddleCenter, outline: false);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        /// <summary>앵커·피벗을 같은 점에 두고 배치.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
