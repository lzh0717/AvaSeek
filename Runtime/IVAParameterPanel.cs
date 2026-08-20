using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IVAAvatar
{
    /// <summary>
    /// Builds a scrollable slider panel - one row per parameter - at runtime, straight
    /// from <see cref="IVAParameters"/>. Add a parameter to IVARenderer and a slider for
    /// it appears here automatically; there is no per-parameter code and nothing to wire
    /// up in the inspector.
    ///
    /// Put this on any GameObject in the scene, point it at an IVARenderer, press Play.
    /// </summary>
    public class IVAParameterPanel : MonoBehaviour
    {
        [Tooltip("The avatar to drive. Auto-found in the scene if left empty.")]
        public IVARenderer avatar;

        [Header("Layout")]
        public float panelWidth = 320f;
        public float rowHeight = 26f;
        [Range(0f, 1f)] public float backgroundAlpha = 0.75f;

        [Header("Behaviour")]
        [Tooltip("Re-read the avatar every frame so sliders follow external changes.")]
        public bool followExternalChanges = true;

        class Row
        {
            public string Name;
            public Slider Slider;
            public Text Value;
        }

        readonly List<Row> _rows = new List<Row>();
        bool _suppressCallback;

        void Start()
        {
            if (avatar == null) avatar = FindObjectOfType<IVARenderer>();
            if (avatar == null)
            {
                Debug.LogWarning("[IVA] IVAParameterPanel: no IVARenderer in the scene.");
                enabled = false;
                return;
            }
            Build();
        }

        void Update()
        {
            if (!followExternalChanges || _rows.Count == 0) return;

            // Mirror external writes (script, optimizer, animation) back into the sliders
            // without re-firing onValueChanged and writing the same value straight back.
            _suppressCallback = true;
            for (int i = 0; i < _rows.Count; i++)
            {
                Row r = _rows[i];
                if (!IVAParameters.TryGet(avatar, r.Name, out float v)) continue;
                if (Mathf.Approximately(r.Slider.value, v)) continue;
                r.Slider.SetValueWithoutNotify(v);
                r.Value.text = Format(v);
            }
            _suppressCallback = false;
        }

        // Construction

        void Build()
        {
            var canvasGO = new GameObject("IVA Parameter Panel",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            // Full-height panel pinned to the left edge.
            RectTransform panel = NewRect("Panel", canvasGO.transform);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.sizeDelta = new Vector2(panelWidth, 0f);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, backgroundAlpha);

            // Scroll view, so every parameter stays reachable on any screen height.
            RectTransform viewport = NewRect("Viewport", panel);
            Stretch(viewport, 8f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = rowHeight;

            IReadOnlyList<string> names = IVAParameters.Names;
            float y = 0f;
            for (int i = 0; i < names.Count; i++)
            {
                AddRow(content, names[i], y);
                y -= rowHeight;
            }
            content.sizeDelta = new Vector2(0f, -y);

            Debug.Log("[IVA] Parameter panel built: " + names.Count + " sliders.");
        }

        void AddRow(RectTransform parent, string name, float y)
        {
            IVAParameters.TryGetRange(name, out float min, out float max);
            IVAParameters.TryGet(avatar, name, out float current);

            RectTransform row = NewRect(name, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(4f, 0f);
            row.offsetMax = new Vector2(-4f, 0f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(row.sizeDelta.x, rowHeight - 4f);

            Text label = NewText(row, name, TextAnchor.MiddleLeft);
            Frame(label.rectTransform, 0f, 0.52f);

            Text valueText = NewText(row, Format(current), TextAnchor.MiddleRight);
            Frame(valueText.rectTransform, 0.82f, 1f);

            Slider slider = BuildSlider(row, min, max, current);
            Frame((RectTransform)slider.transform, 0.54f, 0.80f, 0.25f, 0.75f);

            string captured = name;
            slider.onValueChanged.AddListener(v =>
            {
                if (_suppressCallback) return;
                IVAParameters.TrySet(avatar, captured, v);
                valueText.text = Format(v);
            });

            _rows.Add(new Row { Name = name, Slider = slider, Value = valueText });
        }

        // uGUI's Slider needs its fill/handle rects wired by hand when built from code.
        Slider BuildSlider(RectTransform parent, float min, float max, float value)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var slider = go.GetComponent<Slider>();
            var self = (RectTransform)go.transform;

            RectTransform bg = NewRect("Background", self);
            Stretch(bg, 0f);
            bg.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);

            RectTransform fillArea = NewRect("Fill Area", self);
            Stretch(fillArea, 0f);
            RectTransform fill = NewRect("Fill", fillArea);
            Stretch(fill, 0f);
            fill.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.85f, 1f, 0.9f);

            RectTransform handleArea = NewRect("Handle Slide Area", self);
            Stretch(handleArea, 0f);
            RectTransform handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(10f, 0f);
            handle.gameObject.AddComponent<Image>().color = Color.white;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            return slider;
        }

        // Helpers

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        static void Frame(RectTransform rt, float xMin, float xMax, float yMin = 0f, float yMax = 1f)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text NewText(Transform parent, string content, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.alignment = anchor;
            t.fontSize = 11;
            t.color = Color.white;
            t.font = BuiltinFont();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // Unity renamed the built-in legacy font in 2022.2; try the new name first.
        static Font BuiltinFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        static string Format(float v)
        {
            return v.ToString(Mathf.Abs(v) >= 10f ? "F1" : "F3");
        }
    }
}
