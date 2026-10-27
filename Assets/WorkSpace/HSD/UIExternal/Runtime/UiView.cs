using System;
using System.Collections.Generic;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGD.UI.External
{
    /// <summary>UGUI factory and screen ownership boundary shared with future game adapters.</summary>
    public sealed class UiView : IDisposable
    {
        private readonly IUiBindings _bindings;
        private readonly TMP_FontAsset _font;
        private readonly UiImageSet _images;
        private readonly List<Action> _render = new List<Action>();
        private readonly List<Action> _cleanup = new List<Action>();
        private bool _disposed;
        /// <summary>Staged inactive root; caller activates only after construction succeeds.</summary>
        public GameObject Root { get; private set; }

        private UiView(IUiBindings bindings, TMP_FontAsset font, UiImageSet images)
        { _bindings = bindings; _font = font; _images = images; }

        /// <summary>Creates a complete inactive tree. Construction failure releases all resources it takes ownership of.</summary>
        public static UiView Create(UiDocument document, RectTransform parent, IUiBindings bindings, TMP_FontAsset font, UiImageSet images)
        {
            var view = new UiView(bindings, font, images);
            try
            {
                view.Root = new GameObject((string)document.Screen.Attribute("id"), typeof(RectTransform));
                view.Root.SetActive(false);
                var rect = (RectTransform)view.Root.transform;
                rect.SetParent(parent, false); Stretch(rect);
                view.AddImage(rect, UiXml.Color(document.Screen, "background", new Color(.05f, .05f, .08f)));
                foreach (var child in document.Screen.Elements()) view.Build(child, rect);
                view._bindings.OnChanged += view.Render;
                view.Render();
                return view;
            }
            catch { view.Dispose(); throw; }
        }

        /// <summary>Refreshes values silently so reload never dispatches a command.</summary>
        public void Render() { foreach (var render in _render) render(); }

        private RectTransform Build(XElement node, RectTransform parent)
        {
            string tag = node.Name.LocalName;
            var rect = Rect(UiXml.Required(node, "id"), parent);
            rect.sizeDelta = new Vector2(UiXml.Number(node, "width", 800), UiXml.Number(node, "height", 80));
            rect.anchoredPosition = new Vector2(UiXml.Number(node, "x", 0), UiXml.Number(node, "y", 0));
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = rect.sizeDelta.x; layout.preferredHeight = rect.sizeDelta.y;
            layout.minWidth = 0; layout.minHeight = 0;
            Color color = UiXml.Color(node, "color", Color.white);
            Color background = UiXml.Color(node, "background", Color.clear);
            float fontSize = UiXml.Number(node, "fontSize", 32);
            bool interactable = UiXml.Boolean(node, "interactable", true);
            Image image = null;
            if (node.Attribute("image") != null || background.a > 0 || tag == "Button" || tag == "Toggle")
            {
                image = AddImage(rect, tag == "Toggle" && background.a == 0 ? Color.clear : background.a > 0 ? background : Color.white);
                if (node.Attribute("image") != null)
                {
                    image.sprite = _images.Get((string)node.Attribute("image"));
                    image.type = image.sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
                    image.preserveAspect = tag == "Image" && UiXml.Boolean(node, "preserveAspect", true);
                }
            }
            if (tag == "Column" || tag == "Row")
            {
                HorizontalOrVerticalLayoutGroup group = tag == "Column"
                    ? rect.gameObject.AddComponent<VerticalLayoutGroup>() : rect.gameObject.AddComponent<HorizontalLayoutGroup>();
                int padding = Mathf.RoundToInt(UiXml.Number(node, "padding", 24));
                group.padding = new RectOffset(padding, padding, padding, padding);
                group.spacing = UiXml.Number(node, "gap", 16);
                group.childAlignment = TextAnchor.UpperCenter;
                group.childControlWidth = false; group.childControlHeight = false;
                group.childForceExpandWidth = false; group.childForceExpandHeight = false;
            }
            if (tag == "Text")
            {
                var label = Label(rect, (string)node.Attribute("text") ?? "", color, fontSize);
                string align = (string)node.Attribute("align") ?? "left";
                label.alignment = align == "center" ? TextAlignmentOptions.Center : align == "right" ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                string key = (string)node.Attribute("bind");
                if (key != null) _render.Add(() => label.text = ((string)node.Attribute("text") ?? "") +
                    Convert.ToString(_bindings.Read(key), System.Globalization.CultureInfo.InvariantCulture) + ((string)node.Attribute("suffix") ?? ""));
            }
            if (tag == "Button")
            {
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                Configure(button, interactable);
                Label(rect, (string)node.Attribute("text") ?? "", color, fontSize);
                button.onClick.AddListener(() => _bindings.Execute(UiXml.Required(node, "command")));
                _cleanup.Add(() => button.onClick.RemoveAllListeners());
            }
            if (tag == "Slider") BuildSlider(node, rect, color, interactable);
            if (tag == "Toggle") BuildToggle(node, rect, color, fontSize, interactable);
            foreach (var child in node.Elements()) Build(child, rect);
            return rect;
        }

        private void BuildSlider(XElement node, RectTransform rect, Color color, bool interactable)
        {
            var slider = rect.gameObject.AddComponent<Slider>();
            Configure(slider, interactable);
            var track = Rect("track", rect); Stretch(track, 24, 24, rect.sizeDelta.y * .4f, rect.sizeDelta.y * .4f);
            AddImage(track, new Color(.2f, .22f, .3f));
            var fill = Rect("fill", track); Stretch(fill); AddImage(fill, color);
            var area = Rect("handle-area", rect); Stretch(area, 24, 24, 0, 0);
            var handle = Rect("handle", area); handle.sizeDelta = new Vector2(48, rect.sizeDelta.y);
            var handleImage = AddImage(handle, Color.white);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.minValue = UiXml.Number(node, "min", 0); slider.maxValue = UiXml.Number(node, "max", 100);
            slider.wholeNumbers = UiXml.Boolean(node, "wholeNumbers", false);
            string key = UiXml.Required(node, "bind"), command = UiXml.Required(node, "onChange");
            _render.Add(() => slider.SetValueWithoutNotify(Convert.ToSingle(_bindings.Read(key))));
            slider.onValueChanged.AddListener(value => _bindings.Execute(command, value));
            _cleanup.Add(() => slider.onValueChanged.RemoveAllListeners());
        }

        private void BuildToggle(XElement node, RectTransform rect, Color color, float fontSize, bool interactable)
        {
            var toggle = rect.gameObject.AddComponent<Toggle>(); Configure(toggle, interactable);
            var box = Rect("box", rect); box.anchorMin = box.anchorMax = new Vector2(0, .5f);
            box.pivot = new Vector2(0, .5f); box.sizeDelta = new Vector2(56, 56);
            toggle.targetGraphic = AddImage(box, new Color(.25f, .27f, .34f));
            var check = Rect("check", box); Stretch(check, 10, 10, 10, 10); toggle.graphic = AddImage(check, color);
            var labelRect = Rect("label", rect); Stretch(labelRect, 80, 0, 0, 0);
            var label = Label(labelRect, (string)node.Attribute("text") ?? "", color, fontSize);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            string key = UiXml.Required(node, "bind"), command = UiXml.Required(node, "onChange");
            _render.Add(() => toggle.SetIsOnWithoutNotify((bool)_bindings.Read(key)));
            toggle.onValueChanged.AddListener(value => _bindings.Execute(command, value));
            _cleanup.Add(() => toggle.onValueChanged.RemoveAllListeners());
        }

        private static void Configure(Selectable selectable, bool interactable)
        {
            selectable.interactable = interactable;
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private TextMeshProUGUI Label(RectTransform parent, string text, Color color, float size)
        {
            var rect = Rect("text", parent); Stretch(rect);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font; label.fontSize = size; label.text = text; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.richText = false; label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private Image AddImage(RectTransform parent, Color color)
        { var image = parent.gameObject.AddComponent<Image>(); image.color = color; return image; }

        /// <summary>Creates a centered UI transform without inheriting world-space coordinates.</summary>
        public static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        /// <summary>Stretches a UI rect inside its parent using explicit insets.</summary>
        public static void Stretch(RectTransform rect, float left = 0, float right = 0, float bottom = 0, float top = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }

        /// <summary>Stops subscriptions/listeners before disposing the tree and its external resources.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _bindings.OnChanged -= Render;
            foreach (var cleanup in _cleanup) cleanup();
            _cleanup.Clear(); _render.Clear();
            if (Root != null) { Root.SetActive(false); UnityEngine.Object.Destroy(Root); }
            _images.Dispose();
        }
    }
}
