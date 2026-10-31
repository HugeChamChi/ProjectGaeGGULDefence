using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGD.UI.Authoring
{
    /// <summary>The identical UGUI component graph renderer used by the visual Studio and the production settings host.</summary>
    public sealed class AuthoredUiView : IDisposable
    {
        private static readonly Unity.Profiling.ProfilerMarker _createMarker = new Unity.Profiling.ProfilerMarker("UIAuthoring.Create");
        private static readonly Unity.Profiling.ProfilerMarker _prefabMarker = new Unity.Profiling.ProfilerMarker("UIAuthoring.InstantiateAndBind");
        private readonly AuthoringResources _resources;
        private readonly IAuthoringBindings _bindings;
        private readonly List<Action> _refresh = new List<Action>();
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Dictionary<(Material Base, float Width, UnityEngine.Color Color), Material> _materials = new Dictionary<(Material, float, UnityEngine.Color), Material>();
        private bool _disposed;
        /// <summary>Inactive until every component/reference/state has been prepared.</summary>
        public GameObject Root { get; private set; }
        /// <summary>Stable composed identities, including nested prefab instance paths.</summary>
        public Dictionary<string, RectTransform> Elements { get; } = new Dictionary<string, RectTransform>();
        /// <summary>Source metadata for authoring and source comparisons; null on a baked game view.</summary>
        public ResolvedUiDocument Document { get; private set; }
        /// <summary>Host animation target prepared by source composition or serialized prefab references.</summary>
        public RectTransform AnimationRoot { get; private set; }
        /// <summary>Whether this view was instantiated from a baked prefab without source interpretation.</summary>
        public bool IsBaked { get; private set; }

        private AuthoredUiView(AuthoringResources resources, IAuthoringBindings bindings) { _resources = resources; _bindings = bindings; }

        /// <summary>Builds a hidden candidate; any failure releases all newly owned objects and resources.</summary>
        public static AuthoredUiView Create(ResolvedUiDocument document, RectTransform parent, AuthoringResources resources, IAuthoringBindings bindings)
        {
            using var sample = _createMarker.Auto();
            var view = new AuthoredUiView(resources, bindings) { Document = document };
            try
            {
                view.Root = new GameObject("Authored UI3030", typeof(RectTransform), typeof(CanvasGroup)); view.Root.SetActive(false);
                var root = (RectTransform)view.Root.transform; root.SetParent(parent, false); Stretch(root);
                if ((string)document.Root.Attribute("canvas") == "own")
                {
                    var canvas = view.Root.AddComponent<Canvas>();
                    canvas.overrideSorting = false;
                    var parentCanvas = parent.GetComponentInParent<Canvas>();
                    if (parentCanvas != null) canvas.additionalShaderChannels = parentCanvas.additionalShaderChannels;
                    var raycaster = view.Root.AddComponent<GraphicRaycaster>();
                    var parentRaycaster = parent.GetComponentInParent<GraphicRaycaster>();
                    if (parentRaycaster != null)
                    {
                        raycaster.ignoreReversedGraphics = parentRaycaster.ignoreReversedGraphics;
                        raycaster.blockingObjects = parentRaycaster.blockingObjects;
                        raycaster.blockingMask = parentRaycaster.blockingMask;
                    }
                }
                foreach (var element in document.Root.Elements("Element")) view.CreateTree(element, root);
                foreach (var element in document.Root.Descendants("Element")) view.AddComponents(element);
                foreach (var element in document.Root.Descendants("Element")) view.Wire(element);
                string animation = (string)document.Root.Attribute("animation");
                view.AnimationRoot = animation != null && view.Elements.TryGetValue(animation, out var target) ? target : view.Elements.Values.FirstOrDefault(x => x.name == "Window");
                bindings.OnChanged += view.Refresh; view.Refresh();
                return view;
            }
            catch { view.Dispose(); throw; }
        }

        /// <summary>Clones a native prefab hierarchy and applies permitted visual edits without rebuilding its objects.</summary>
        public static AuthoredUiView FromNative(NativePrefabTemplate template, ResolvedUiDocument document, RectTransform parent, AuthoringResources resources, IAuthoringBindings bindings)
        {
            var view = new AuthoredUiView(resources, bindings) { Document = document, IsBaked = true };
            try
            {
                view.Root = UnityEngine.Object.Instantiate(template.gameObject, parent, false); view.Root.SetActive(false);
                var copy = view.Root.GetComponent<NativePrefabTemplate>();
                for (int i = 0; i < copy.Ids.Length; i++) view.Elements.Add(copy.Ids[i], copy.Rects[i]);
                if (view.Root.GetComponent<CanvasGroup>() == null) view.Root.AddComponent<CanvasGroup>();
                foreach (var node in document.Root.Descendants("Element"))
                {
                    var rect = view.Elements[AuthoringWorkspace.Required(node, "id")];
                    ApplyRect(rect, node); rect.name = (string)node.Attribute("name");
                    rect.gameObject.SetActive(Boolean(node, "active", true));
                    string visible = (string)node.Attribute("visible");
                    if (visible != null) view._refresh.Add(() => rect.gameObject.SetActive(Boolean(node, "active", true) && Convert.ToBoolean(bindings.Read(visible))));
                    view.AddComponents(node);
                }
                foreach (var node in document.Root.Descendants("Element")) view.Wire(node);
                view.AnimationRoot = view.Elements[copy.AnimationId];
                var motion = view.Root.AddComponent<NativePrefabMotion>(); motion.Configure(copy, view.AnimationRoot);
                bindings.OnChanged += view.Refresh; view.Refresh(); return view;
            }
            catch { view.Dispose(); throw; }
        }

        /// <summary>Clones the complete serialized graph, binds live services while hidden, and never resolves XML.</summary>
        public static AuthoredUiView FromPrefab(BakedUiDocument prefab, RectTransform parent, IAuthoringBindings bindings)
        {
            using var sample = _prefabMarker.Auto();
            if (prefab == null) throw new FormatException("Baked UI is missing. Install the current publication in Unity.");
            var view = new AuthoredUiView(null, bindings) { IsBaked = true };
            try
            {
                view.Root = UnityEngine.Object.Instantiate(prefab.gameObject, parent, false); view.Root.SetActive(false);
                var document = view.Root.GetComponent<BakedUiDocument>();
                document.Bind(bindings, view.Elements, view._refresh, view._cleanup); view.AnimationRoot = document.AnimationRoot;
                var canvas = view.Root.GetComponent<Canvas>(); var parentCanvas = parent.GetComponentInParent<Canvas>();
                if (canvas != null && parentCanvas != null) canvas.additionalShaderChannels |= parentCanvas.additionalShaderChannels;
                var raycaster = view.Root.GetComponent<GraphicRaycaster>(); var parentRaycaster = parent.GetComponentInParent<GraphicRaycaster>();
                if (raycaster != null && parentRaycaster != null)
                { raycaster.ignoreReversedGraphics = parentRaycaster.ignoreReversedGraphics; raycaster.blockingObjects = parentRaycaster.blockingObjects; raycaster.blockingMask = parentRaycaster.blockingMask; }
                bindings.OnChanged += view.Refresh; view.Refresh(); return view;
            }
            catch { view.Dispose(); throw; }
        }

        /// <summary>Silently refreshes from services. It never invokes button or settings commands.</summary>
        public void Refresh() { foreach (var action in _refresh) action(); }
        /// <summary>Editing captures the pointer in Studio; testing/gameplay uses the original UGUI input.</summary>
        public void SetInput(bool enabled) { var group = Root.GetComponent<CanvasGroup>(); group.interactable = enabled; group.blocksRaycasts = enabled; }

        private void CreateTree(XElement source, RectTransform parent)
        {
            var rect = new GameObject((string)source.Attribute("name") ?? (string)source.Attribute("key"), typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); ApplyRect(rect, source); Elements.Add(AuthoringWorkspace.Required(source, "id"), rect);
            foreach (var child in source.Elements("Element")) CreateTree(child, rect);
            bool active = Boolean(source, "active", true); rect.gameObject.SetActive(active);
            string visibility = (string)source.Attribute("visible");
            if (visibility != null) _refresh.Add(() => rect.gameObject.SetActive(active && Convert.ToBoolean(_bindings.Read(visibility))));
        }

        /// <summary>Applies exact UGUI anchors, pivot, size delta, rotation and scale without flattening layout ownership.</summary>
        public static void ApplyRect(RectTransform rect, XElement source)
        {
            rect.anchorMin = new Vector2(N(source, "anchorMinX", .5f), N(source, "anchorMinY", .5f));
            rect.anchorMax = new Vector2(N(source, "anchorMaxX", .5f), N(source, "anchorMaxY", .5f));
            rect.pivot = new Vector2(N(source, "pivotX", .5f), N(source, "pivotY", .5f));
            rect.sizeDelta = new Vector2(N(source, "width", 100), N(source, "height", 100));
            rect.anchoredPosition = new Vector2(N(source, "x"), N(source, "y"));
            rect.localScale = new Vector3(N(source, "scaleX", 1), N(source, "scaleY", 1), 1);
            rect.localRotation = Quaternion.Euler(0, 0, N(source, "rotation"));
        }

        private void AddComponents(XElement source)
        {
            var target = Elements[AuthoringWorkspace.Required(source, "id")].gameObject;
            foreach (var item in source.Elements().Where(x => x.Name != "Element"))
            {
                switch (item.Name.LocalName)
                {
                    case "Image":
                        var image = (target.GetComponent<Image>() ?? target.AddComponent<Image>()); image.sprite = _resources.Sprite((string)item.Attribute("sprite"));
                        image.color = Color(item, "color", UnityEngine.Color.white); image.type = (Image.Type)(int)N(item, "type");
                        image.preserveAspect = Boolean(item, "preserveAspect"); image.raycastTarget = UiPublishLint.ImageRaycastTarget(item);
                        image.fillMethod = (Image.FillMethod)(int)N(item, "fillMethod", 4); image.fillAmount = N(item, "fillAmount", 1);
                        image.fillOrigin = (int)N(item, "fillOrigin"); image.fillClockwise = Boolean(item, "clockwise", true);
                        image.pixelsPerUnitMultiplier = N(item, "ppuMultiplier", 1); break;
                    case "Text":
                        var text = (target.GetComponent<TextMeshProUGUI>() ?? target.AddComponent<TextMeshProUGUI>()); text.font = _resources.Font(AuthoringWorkspace.Required(item, "font"));
                        text.text = (string)item.Attribute("text") ?? ""; text.fontSize = N(item, "size", 32); text.color = Color(item, "color", UnityEngine.Color.white);
                        text.alignment = (TextAlignmentOptions)(int)N(item, "alignment", (int)TextAlignmentOptions.Center);
                        text.fontStyle = (FontStyles)(int)N(item, "style"); text.enableAutoSizing = Boolean(item, "autoSize");
                        text.fontSizeMin = N(item, "minSize", 18); text.fontSizeMax = N(item, "maxSize", 72);
                        text.textWrappingMode = (TextWrappingModes)(int)N(item, "wrap"); text.overflowMode = (TextOverflowModes)(int)N(item, "overflow");
                        text.richText = Boolean(item, "rich", true); text.raycastTarget = Boolean(item, "raycast");
                        text.margin = new Vector4(N(item, "marginLeft"), N(item, "marginTop"), N(item, "marginRight"), N(item, "marginBottom"));
                        if (N(item, "outlineWidth") > 0)
                        {
                            var key = (text.fontSharedMaterial, N(item, "outlineWidth"), Color(item, "outlineColor", UnityEngine.Color.black));
                            if (!_materials.TryGetValue(key, out var material))
                            {
                                material = new Material(key.Item1) { name = "Authored outline" };
                                // Hidden candidates have not run TMP.Awake; configure the material directly.
                                material.SetFloat("_OutlineWidth", key.Item2); material.SetColor("_OutlineColor", key.Item3);
                                _materials.Add(key, material);
                            }
                            text.fontSharedMaterial = material;
                        }
                        string textState = (string)item.Attribute("bind");
                        if (!string.IsNullOrEmpty(textState)) _refresh.Add(() => text.text = Convert.ToString(_bindings.Read(textState), CultureInfo.InvariantCulture));
                        break;
                    case "Button": _ = target.GetComponent<Button>() ?? target.AddComponent<Button>(); break;
                    case "Slider": _ = target.GetComponent<Slider>() ?? target.AddComponent<Slider>(); break;
                    case "Toggle": _ = target.GetComponent<Toggle>() ?? target.AddComponent<Toggle>(); break;
                    case "Switch": _ = target.GetComponent<AuthoredSwitch>() ?? target.AddComponent<AuthoredSwitch>(); break;
                    case "CanvasGroup":
                        var group = (target.GetComponent<CanvasGroup>() ?? target.AddComponent<CanvasGroup>()); group.alpha = N(item, "alpha", 1); group.interactable = Boolean(item, "interactable", true); group.blocksRaycasts = Boolean(item, "blocksRaycasts", true); break;
                    case "Mask": (target.GetComponent<Mask>() ?? target.AddComponent<Mask>()).showMaskGraphic = Boolean(item, "showGraphic", true); break;
                    case "RectMask": _ = target.GetComponent<RectMask2D>() ?? target.AddComponent<RectMask2D>(); break;
                    case "Layout":
                        HorizontalOrVerticalLayoutGroup layout = (string)item.Attribute("axis") == "vertical" ? (target.GetComponent<VerticalLayoutGroup>() ?? target.AddComponent<VerticalLayoutGroup>()) : (target.GetComponent<HorizontalLayoutGroup>() ?? target.AddComponent<HorizontalLayoutGroup>());
                        layout.padding = new RectOffset((int)N(item, "paddingLeft"), (int)N(item, "paddingRight"), (int)N(item, "paddingTop"), (int)N(item, "paddingBottom"));
                        layout.spacing = N(item, "gap"); layout.childAlignment = (TextAnchor)(int)N(item, "alignment");
                        layout.childControlWidth = Boolean(item, "controlWidth", true); layout.childControlHeight = Boolean(item, "controlHeight", true);
                        layout.childForceExpandWidth = Boolean(item, "expandWidth"); layout.childForceExpandHeight = Boolean(item, "expandHeight"); break;
                    case "LayoutElement":
                        var element = (target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>()); element.ignoreLayout = Boolean(item, "ignore"); element.minWidth = N(item, "minWidth", -1); element.minHeight = N(item, "minHeight", -1);
                        element.preferredWidth = N(item, "preferredWidth", -1); element.preferredHeight = N(item, "preferredHeight", -1); element.flexibleWidth = N(item, "flexibleWidth", -1); element.flexibleHeight = N(item, "flexibleHeight", -1); break;
                    case "SizeFitter": var fitter = (target.GetComponent<ContentSizeFitter>() ?? target.AddComponent<ContentSizeFitter>()); fitter.horizontalFit = (ContentSizeFitter.FitMode)(int)N(item, "horizontal"); fitter.verticalFit = (ContentSizeFitter.FitMode)(int)N(item, "vertical"); break;
                    default: throw new FormatException("Unsupported component " + item.Name);
                }
            }
        }

        private void Wire(XElement source)
        {
            var rect = Elements[AuthoringWorkspace.Required(source, "id")];
            foreach (var item in source.Elements())
            {
                if (item.Name == "Button")
                {
                    var button = rect.GetComponent<Button>(); Configure(button, item); string command = (string)item.Attribute("command");
                    if (!string.IsNullOrEmpty(command)) button.onClick.AddListener(() => _bindings.Execute(command));
                    _cleanup.Add(() => button.onClick.RemoveAllListeners());
                }
                else if (item.Name == "Slider")
                {
                    var slider = rect.GetComponent<Slider>(); Configure(slider, item);
                    slider.direction = (Slider.Direction)(int)N(item, "direction"); slider.minValue = N(item, "min"); slider.maxValue = N(item, "max", 1);
                    slider.wholeNumbers = Boolean(item, "wholeNumbers"); slider.fillRect = Ref(item, "fill"); slider.handleRect = Ref(item, "handle");
                    slider.SetValueWithoutNotify(N(item, "value"));
                    string state = (string)item.Attribute("bind"), command = (string)item.Attribute("command");
                    if (!string.IsNullOrEmpty(state)) _refresh.Add(() => slider.SetValueWithoutNotify(Convert.ToSingle(_bindings.Read(state))));
                    if (!string.IsNullOrEmpty(command)) slider.onValueChanged.AddListener(x => _bindings.Execute(command, x));
                    _cleanup.Add(() => slider.onValueChanged.RemoveAllListeners());
                }
                else if (item.Name == "Toggle")
                {
                    var toggle = rect.GetComponent<Toggle>(); Configure(toggle, item); toggle.graphic = Ref(item, "graphic")?.GetComponent<Graphic>();
                    toggle.toggleTransition = Toggle.ToggleTransition.None; toggle.SetIsOnWithoutNotify(Boolean(item, "value", true));
                    string state = (string)item.Attribute("bind"), command = (string)item.Attribute("command");
                    var on = Ref(item, "on"); var off = Ref(item, "off"); var visualSlider = Ref(item, "visualSlider")?.GetComponent<Slider>();
                    void RefreshVisual()
                    {
                        bool value = string.IsNullOrEmpty(state) ? toggle.isOn : Convert.ToBoolean(_bindings.Read(state));
                        toggle.SetIsOnWithoutNotify(value); if (on != null) on.gameObject.SetActive(value); if (off != null) off.gameObject.SetActive(!value);
                        if (visualSlider != null) visualSlider.SetValueWithoutNotify(value ? 1 : 0);
                    }
                    _refresh.Add(RefreshVisual);
                    if (!string.IsNullOrEmpty(command)) toggle.onValueChanged.AddListener(value => { _bindings.Execute(command, value); RefreshVisual(); });
                    _cleanup.Add(() => toggle.onValueChanged.RemoveAllListeners());
                }
                else if (item.Name == "Switch")
                {
                    var control = rect.GetComponent<AuthoredSwitch>(); control.Interactable = Boolean(item, "interactable", true);
                    var visual = Ref(item, "visualSlider")?.GetComponent<Slider>(); if (visual != null) visual.interactable = false;
                    var on = Ref(item, "on"); var off = Ref(item, "off");
                    string state = AuthoringWorkspace.Required(item, "bind"), command = AuthoringWorkspace.Required(item, "command");
                    _refresh.Add(() => { bool value = Convert.ToBoolean(_bindings.Read(state)); if (visual != null) visual.SetValueWithoutNotify(value ? 1 : 0); if (on != null) on.gameObject.SetActive(value); if (off != null) off.gameObject.SetActive(!value); });
                    control.OnPressed = () => _bindings.Execute(command, !Convert.ToBoolean(_bindings.Read(state)));
                    _cleanup.Add(() => control.OnPressed = null);
                }
            }
        }

        private void Configure(Selectable control, XElement item)
        {
            control.navigation = new Navigation { mode = Navigation.Mode.None };
            control.targetGraphic = Ref(item, "target")?.GetComponent<Graphic>() ?? control.GetComponent<Graphic>();
            control.interactable = Boolean(item, "interactable", true);
            control.transition = (Selectable.Transition)(int)N(item, "transition", 1);
            var colors = control.colors; colors.normalColor = Color(item, "normal", colors.normalColor); colors.highlightedColor = Color(item, "highlighted", colors.highlightedColor);
            colors.pressedColor = Color(item, "pressed", colors.pressedColor); colors.selectedColor = Color(item, "selected", colors.selectedColor); colors.disabledColor = Color(item, "disabled", colors.disabledColor); control.colors = colors;
            string enabled = (string)item.Attribute("enabled");
            if (enabled != null) _refresh.Add(() => control.interactable = Convert.ToBoolean(_bindings.Read(enabled)));
        }
        private RectTransform Ref(XElement node, string key) => !string.IsNullOrEmpty((string)node.Attribute(key)) ? Elements[(string)node.Attribute(key)] : null;
        private static float N(XElement node, string key, float fallback = 0) => AuthoringWorkspace.Number(node, key, fallback);
        /// <summary>Parses strict optional boolean attributes.</summary>
        public static bool Boolean(XElement node, string key, bool fallback = false) => node.Attribute(key) == null ? fallback : bool.Parse((string)node.Attribute(key));
        /// <summary>Parses explicit HTML colors.</summary>
        public static UnityEngine.Color Color(XElement node, string key, UnityEngine.Color fallback) => node.Attribute(key) == null ? fallback : ColorUtility.TryParseHtmlString((string)node.Attribute(key), out var color) ? color : throw new FormatException("Invalid color " + key);
        /// <summary>Stretches a new UI root inside a host.</summary>
        public static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        /// <summary>Unsubscribes and removes controls before releasing external resources and this view's shared materials.</summary>
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _bindings.OnChanged -= Refresh;
            foreach (var cleanup in _cleanup) cleanup(); _cleanup.Clear(); _refresh.Clear();
            if (Root != null) { Root.SetActive(false); DestroyOwned(Root); }
            foreach (var material in _materials.Values) DestroyOwned(material); _materials.Clear(); _resources?.Dispose();
        }
        private static void DestroyOwned(UnityEngine.Object value)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (UnityEditor.EditorUtility.IsPersistent(value)) throw new InvalidOperationException("A view cannot destroy an asset it does not own");
                UnityEngine.Object.DestroyImmediate(value); return;
            }
#endif
            UnityEngine.Object.Destroy(value);
        }
    }
}
