// UIFactory — builds uGUI elements from code so the scenes stay nearly empty
// (easier to read in a lesson, and no broken references when files move).
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MazeNav
{
    public static class UIFactory
    {
        static Font font;
        public static Font DefaultFont => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// Screen overlay for desktop, or a world-space panel (metres) for VR.
        public static Canvas CreateCanvas(string name, bool worldSpace, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            var scaler = go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            if (worldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = size;
                go.transform.localScale = Vector3.one * 0.0015f;   // 1000 px ≈ 1.5 m
                scaler.dynamicPixelsPerUnit = 3;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600, 900);
                scaler.matchWidthOrHeight = 0.5f;
            }
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax,
                                  Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
                                 TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string label, Color bg, Vector2 anchorMin,
                                    Vector2 anchorMax, UnityEngine.Events.UnityAction onClick, int fontSize = 30)
        {
            var img = Panel(name, parent, bg, anchorMin, anchorMax);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            b.colors = colors;
            b.onClick.AddListener(onClick);
            Label("Label", img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            return b;
        }

        /// A white up-pointing triangle sprite, generated in code (used for the GPS arrow).
        public static Sprite ArrowSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float half = (1f - y / (float)n) * n * 0.5f;          // wide at the bottom, point at the top
                bool head = y > n * 0.35f && Mathf.Abs(x - n / 2f) < half;
                bool shaft = y <= n * 0.35f && Mathf.Abs(x - n / 2f) < n * 0.14f;
                tex.SetPixel(x, y, head || shaft ? Color.white : Color.clear);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
