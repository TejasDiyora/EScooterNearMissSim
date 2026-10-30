using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EScooterSim
{
    /// <summary>
    /// Builds Unity UI elements from code, so the interface needs no prefabs or sprites.
    /// Sizes are in the canvas's reference units (1600 x 900) and scale with the window.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.09f, 0.12f, 0.94f);
        public static readonly Color TileColor = new Color(0.14f, 0.17f, 0.22f, 1f);
        public static readonly Color TrackColor = new Color(0.26f, 0.3f, 0.37f, 1f);
        public static readonly Color PrimaryButton = new Color(0.16f, 0.46f, 0.9f, 1f);
        public static readonly Color SecondaryButton = new Color(0.27f, 0.31f, 0.39f, 1f);
        public static readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f, 1f);
        public static readonly Color MutedText = new Color(0.67f, 0.72f, 0.79f, 1f);
        public static readonly Color Accent = new Color(1f, 0.8f, 0.25f, 1f);
        public static readonly Color Danger = new Color(1f, 0.42f, 0.36f, 1f);

        private static Font font;

        public static Font DefaultFont
        {
            get
            {
                if (font == null)
                {
#if UNITY_2022_2_OR_NEWER
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return font;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// Buttons and sliders need an EventSystem with an input module. Which module works
        /// depends on the project's Active Input Handling setting, so this picks the right one.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#else
            System.Type moduleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null) go.AddComponent(moduleType);
            else go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>A plain coloured rectangle.</summary>
        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        public static Text CreateText(Transform parent, string content, int size, TextAnchor anchor, Color color)
        {
            RectTransform rect = CreateRect(parent, "Text");
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Color color, UnityAction onClick)
        {
            RectTransform rect = CreatePanel(parent, "Button " + label, color);
            Image image = rect.GetComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text text = CreateText(rect, label, 17, TextAnchor.MiddleCenter, TextColor);
            Stretch(text.rectTransform);

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>A horizontal slider built from plain rectangles.</summary>
        public static Slider CreateSlider(Transform parent, float min, float max, bool wholeNumbers)
        {
            RectTransform rect = CreateRect(parent, "Slider");
            Slider slider = rect.gameObject.AddComponent<Slider>();

            RectTransform track = CreatePanel(rect, "Background", TrackColor);
            SetAnchors(track, new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));

            RectTransform fillArea = CreateRect(rect, "Fill Area");
            SetAnchors(fillArea, new Vector2(0f, 0.36f), new Vector2(1f, 0.64f));
            RectTransform fill = CreatePanel(fillArea, "Fill", PrimaryButton);
            fill.GetComponent<Image>().raycastTarget = false;
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = Vector2.zero;

            RectTransform handleArea = CreateRect(rect, "Handle Slide Area");
            SetAnchors(handleArea, Vector2.zero, Vector2.one);
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);
            RectTransform handle = CreatePanel(handleArea, "Handle", Color.white);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(16f, 0f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            return slider;
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        /// <summary>Fixes a rectangle's size and pins it to one point of its parent.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Stacks the children of <paramref name="go"/> top to bottom, each as wide as the parent.</summary>
        public static VerticalLayoutGroup AddVerticalLayout(GameObject go, int padding, float spacing)
        {
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>Lines the children of <paramref name="go"/> up left to right, each as tall as the parent.</summary>
        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing)
        {
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }

        /// <summary>
        /// Tells the parent layout how big this element wants to be.
        /// Pass a negative width or height to leave that dimension to the layout.
        /// </summary>
        public static LayoutElement SetSize(GameObject go, float width, float height, float flexibleWidth)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                element.minWidth = width;
                element.preferredWidth = width;
            }
            if (height >= 0f)
            {
                element.minHeight = height;
                element.preferredHeight = height;
            }
            element.flexibleWidth = flexibleWidth;
            return element;
        }

        /// <summary>An empty row of the given height whose children are laid out left to right.</summary>
        public static RectTransform CreateRow(Transform parent, string name, float height, float spacing)
        {
            RectTransform row = CreateRect(parent, name);
            AddHorizontalLayout(row.gameObject, spacing);
            SetSize(row.gameObject, -1f, height, 1f);
            return row;
        }
    }
}
