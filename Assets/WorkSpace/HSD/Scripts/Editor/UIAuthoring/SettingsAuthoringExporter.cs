using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using GGD.UI.Authoring;
using HSD.UI.Setting;
using HSD.UI.Utils;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One-way Unity-resolved extraction of the actual shared UI3030. Never parses prefab YAML.</summary>
public static class SettingsAuthoringExporter
{
    private const string Source = "Assets/WorkSpace/HSD/Prefab/UI/InGame/InGame_Setting_Popup.prefab";
    private const string Destination = "Data/UI/Authoring";
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, TMP_FontAsset> Fonts = new Dictionary<string, TMP_FontAsset>();
    private static readonly List<string> Report = new List<string>();
    private static Dictionary<Transform, string> _ids;
    private static Transform _root;

    /// <summary>Exports only when no authoring originals exist, protecting subsequent designer work.</summary>
    public static void Export()
    {
        if (File.Exists(Destination + "/kit.xml")) throw new InvalidOperationException("Authoring originals already exist. Export refuses to overwrite designer work.");
        Directory.CreateDirectory(Destination + "/Screens"); Directory.CreateDirectory(Destination + "/Prefabs"); Directory.CreateDirectory(Destination + "/Images");
        Sprites.Clear(); Fonts.Clear(); Report.Clear();
        GameObject root = PrefabUtility.LoadPrefabContents(Source);
        try
        {
            _root = root.transform;
            _ids = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(x => (Transform)x, x => Identity(AnimationUtility.CalculateTransformPath(x, _root)));
            var panel = root.GetComponent<UI_SettingPanel_InGame>();
            if (panel == null) throw new InvalidOperationException("Actual shared settings host not found");
            var confirm = Reference<UI_Popup_Confirm>(panel, "confirmPopup");
            var nodes = Serialize((RectTransform)root.transform, confirm?.transform);
            nodes.SetAttributeValue("active", "true");
            // Host wrappers already own safe-area fitting and root placement.
            foreach (var pair in new Dictionary<string, string> { ["x"] = "0", ["y"] = "0", ["width"] = "0", ["height"] = "0", ["anchorMinX"] = "0", ["anchorMinY"] = "0", ["anchorMaxX"] = "1", ["anchorMaxY"] = "1", ["scaleX"] = "1", ["scaleY"] = "1" }) nodes.SetAttributeValue(pair.Key, pair.Value);
            BindButton(nodes, Reference<Button>(panel, "btn_Close"), "settings.close", "closeButton");
            BindButton(nodes, Reference<Button>(panel, "btn_BackgroundClose"), "settings.close", "backgroundClose");
            BindButton(nodes, Reference<Button>(panel, "btn_Restart"), "settings.restart", "restartButton", true);
            BindButton(nodes, Reference<Button>(panel, "btn_GoToLobby"), "settings.lobby", "lobbyButton", true);
            var language = Reference<Button>(panel, "btn_Language");
            if (language != null) { Find(nodes, language.transform)?.SetAttributeValue("active", "false"); Report.Add("BLOCKED incomplete language command: not exposed as a completed feature"); }
            BindSwitch(nodes, Reference<UI_SlidingToggle>(panel, "toggle_Vibration"), "settings.vibration", "settings.setVibration");
            BindSwitch(nodes, Reference<UI_SlidingToggle>(panel, "toggle_DamageFloater"), "settings.damageNumbers", "settings.setDamageNumbers");
            foreach (var frame in root.GetComponentsInChildren<UI_FrameRateSelector>(true))
            {
                var previous = Reference<Button>(frame, "btn_Prev"); var next = Reference<Button>(frame, "btn_Next"); var value = Reference<TMP_Text>(frame, "txt_Value");
                BindButton(nodes, previous, "settings.frame.previous", "framePrevious"); BindButton(nodes, next, "settings.frame.next", "frameNext");
                Find(nodes, previous?.transform)?.Element("Button")?.SetAttributeValue("enabled", "settings.frame.canPrevious");
                Find(nodes, next?.transform)?.Element("Button")?.SetAttributeValue("enabled", "settings.frame.canNext");
                Find(nodes, value?.transform)?.Element("Text")?.SetAttributeValue("bind", "settings.frameRate");
            }
            var rows = root.GetComponentsInChildren<UI_SoundSettingSlot>(true);
            if (rows.Length < 2) throw new InvalidOperationException("Expected real BGM and SFX rows");
            foreach (var row in rows) BindSound(nodes, row);
            XElement original = new XElement(Find(nodes, rows[0].transform));
            var parameters = new XElement("Parameters");
            foreach (var pair in new Dictionary<string, string> { ["volumeState"] = "settings.bgm.volume", ["volumeCommand"] = "settings.bgm.setVolume", ["enabledState"] = "settings.bgm.enabled", ["enabledCommand"] = "settings.bgm.setEnabled" })
                parameters.Add(new XElement("Parameter", new XAttribute("key", pair.Key), new XAttribute("default", pair.Value)));
            original.Descendants("Slider").First(x => x.Attribute("bind") != null).SetAttributeValue("bind", "${volumeState}");
            original.Descendants("Slider").First(x => x.Attribute("command") != null).SetAttributeValue("command", "${volumeCommand}");
            original.Descendants("Toggle").First(x => x.Attribute("bind") != null).SetAttributeValue("bind", "${enabledState}");
            original.Descendants("Toggle").First(x => x.Attribute("command") != null).SetAttributeValue("command", "${enabledCommand}");
            Save("Prefabs/settings.soundRow.xml", new XElement("Prefab", new XAttribute("schema", "2"), new XAttribute("key", "settings.soundRow"), new XAttribute("width", 1080), new XAttribute("height", 1920), parameters, original));
            foreach (var row in rows)
            {
                var actual = Find(nodes, row.transform); string group = row.Group.ToString().ToLowerInvariant();
                var instance = new XElement("Instance", new XAttribute("id", Identity("instance:" + AnimationUtility.CalculateTransformPath(row.transform, _root))), new XAttribute("key", group + "Row"), new XAttribute("prefab", "settings.soundRow"));
                foreach (var pair in new Dictionary<string, string> { ["volumeState"] = "settings." + group + ".volume", ["volumeCommand"] = "settings." + group + ".setVolume", ["enabledState"] = "settings." + group + ".enabled", ["enabledCommand"] = "settings." + group + ".setEnabled" })
                    instance.Add(new XElement("Input", new XAttribute("key", pair.Key), new XAttribute("value", pair.Value)));
                Diff(original, actual, instance); actual.ReplaceWith(instance);
            }
            Save("Screens/UI3030.xml", new XElement("Screen", new XAttribute("schema", "2"), new XAttribute("key", "UI3030"), new XAttribute("width", 1080), new XAttribute("height", 1920), new XAttribute("animation", _ids[Reference<GaeGGUL.Animation.Anim_InOutBase>(panel, "targetAnim").transform]), nodes));
            Save("kit.xml", new XElement("Kit", new XAttribute("schema", "2"), new XAttribute("runtime", "2"), new XAttribute("project", "GaeGGULDefence"), new XAttribute("id", Identity(Source)),
                new XElement("Document", new XAttribute("path", "Screens/UI3030.xml")), new XElement("Document", new XAttribute("path", "Prefabs/settings.soundRow.xml"))));
            Save("catalog.xml", Catalog());
            Save("images.xml", new XElement("Images", Sprites.Select(x => ExportImage(x.Key, x.Value))));
            Save("fonts.xml", new XElement("Fonts", Fonts.Select(x => new XElement("Font", new XAttribute("key", x.Key), new XAttribute("asset", AssetDatabase.GetAssetPath(x.Value))))));
            AssetDatabase.ExportPackage(Fonts.Values.Select(AssetDatabase.GetAssetPath).Distinct().ToArray(), "outputs/ui-authoring-fonts.unitypackage", ExportPackageOptions.IncludeDependencies);
            CompareUse("Assets/WorkSpace/USW/Prefabs/UI/LobbySettingsPopup.prefab");
            CompareUse("Assets/WorkSpace/USW/Prefab/UI/IngameOptionsMenu.prefab");
            Directory.CreateDirectory("outputs/ui-authoring-verification");
            File.WriteAllLines("outputs/ui-authoring-verification/extraction-report.txt", Report);
            UiComposer.Resolve(AuthoringWorkspace.Open(Destination));
            Debug.Log("UI_AUTHORING_EXPORT_DONE " + Sprites.Count + " sprites, " + Fonts.Count + " exact fonts");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static XElement Serialize(RectTransform rect, Transform excluded)
    {
        if (rect == excluded) return null;
        var node = new XElement("Element", new XAttribute("id", _ids[rect]), new XAttribute("name", rect.name), new XAttribute("key", rect.name));
        A(node, "x", rect.anchoredPosition.x); A(node, "y", rect.anchoredPosition.y); A(node, "width", rect.sizeDelta.x); A(node, "height", rect.sizeDelta.y);
        A(node, "anchorMinX", rect.anchorMin.x); A(node, "anchorMinY", rect.anchorMin.y); A(node, "anchorMaxX", rect.anchorMax.x); A(node, "anchorMaxY", rect.anchorMax.y);
        A(node, "pivotX", rect.pivot.x); A(node, "pivotY", rect.pivot.y); A(node, "scaleX", rect.localScale.x); A(node, "scaleY", rect.localScale.y); A(node, "rotation", rect.localEulerAngles.z); A(node, "active", rect.gameObject.activeSelf);
        foreach (var component in rect.GetComponents<Component>())
        {
            if (component == null) throw new FormatException("Missing component at " + rect.name);
            XElement data = null;
            switch (component)
            {
                case RectTransform _: case CanvasRenderer _: break;
                case Image image:
                    data = new XElement("Image"); A(data, "sprite", SpriteKey(image.sprite)); A(data, "color", C(image.color)); A(data, "type", (int)image.type); A(data, "preserveAspect", image.preserveAspect); A(data, "raycast", image.raycastTarget);
                    A(data, "fillMethod", (int)image.fillMethod); A(data, "fillAmount", image.fillAmount); A(data, "fillOrigin", image.fillOrigin); A(data, "clockwise", image.fillClockwise); A(data, "ppuMultiplier", image.pixelsPerUnitMultiplier); break;
                case TMP_Text text:
                    if (text.font == null) throw new FormatException("Missing TMP font " + rect.name);
                    Fonts[text.font.name] = text.font;
                    data = new XElement("Text"); A(data, "font", text.font.name); A(data, "text", text.text); A(data, "size", text.fontSize); A(data, "color", C(text.color)); A(data, "alignment", (int)text.alignment);
                    A(data, "style", (int)text.fontStyle); A(data, "autoSize", text.enableAutoSizing); A(data, "minSize", text.fontSizeMin); A(data, "maxSize", text.fontSizeMax); A(data, "wrap", (int)text.textWrappingMode); A(data, "overflow", (int)text.overflowMode); A(data, "rich", text.richText); A(data, "raycast", text.raycastTarget);
                    A(data, "marginLeft", text.margin.x); A(data, "marginTop", text.margin.y); A(data, "marginRight", text.margin.z); A(data, "marginBottom", text.margin.w); A(data, "outlineWidth", text.outlineWidth); A(data, "outlineColor", C(text.outlineColor)); break;
                case Button button: data = new XElement("Button"); Selectable(data, button); break;
                case Slider slider:
                    data = new XElement("Slider"); Selectable(data, slider); Ref(data, "fill", slider.fillRect); Ref(data, "handle", slider.handleRect); A(data, "min", slider.minValue); A(data, "max", slider.maxValue); A(data, "wholeNumbers", slider.wholeNumbers); A(data, "value", slider.value); A(data, "direction", (int)slider.direction); break;
                case Toggle toggle: data = new XElement("Toggle"); Selectable(data, toggle); Ref(data, "graphic", toggle.graphic); A(data, "value", toggle.isOn); break;
                case UI_SlidingToggle toggle:
                    data = new XElement("Switch"); Ref(data, "visualSlider", rect); Ref(data, "on", Reference<GameObject>(toggle, "backgroundOn")); Ref(data, "off", Reference<GameObject>(toggle, "backgroundOff")); break;
                case CanvasGroup group: data = new XElement("CanvasGroup"); A(data, "alpha", group.alpha); A(data, "interactable", group.interactable); A(data, "blocksRaycasts", group.blocksRaycasts); break;
                case RectMask2D _: data = new XElement("RectMask"); break;
                case Mask mask: data = new XElement("Mask"); A(data, "showGraphic", mask.showMaskGraphic); break;
                case HorizontalOrVerticalLayoutGroup layout:
                    data = new XElement("Layout"); A(data, "axis", layout is VerticalLayoutGroup ? "vertical" : "horizontal"); A(data, "gap", layout.spacing); A(data, "paddingLeft", layout.padding.left); A(data, "paddingRight", layout.padding.right); A(data, "paddingTop", layout.padding.top); A(data, "paddingBottom", layout.padding.bottom); A(data, "alignment", (int)layout.childAlignment);
                    A(data, "controlWidth", layout.childControlWidth); A(data, "controlHeight", layout.childControlHeight); A(data, "expandWidth", layout.childForceExpandWidth); A(data, "expandHeight", layout.childForceExpandHeight); break;
                case LayoutElement layout: data = new XElement("LayoutElement"); A(data, "ignore", layout.ignoreLayout); A(data, "minWidth", layout.minWidth); A(data, "minHeight", layout.minHeight); A(data, "preferredWidth", layout.preferredWidth); A(data, "preferredHeight", layout.preferredHeight); A(data, "flexibleWidth", layout.flexibleWidth); A(data, "flexibleHeight", layout.flexibleHeight); break;
                case ContentSizeFitter fitter: data = new XElement("SizeFitter"); A(data, "horizontal", (int)fitter.horizontalFit); A(data, "vertical", (int)fitter.verticalFit); break;
                case UI_SettingPanel_Base _: case UI_SoundSettingSlot _: case UI_FrameRateSelector _: case GaeGGUL.Animation.Anim_InOutBase _:
                    Report.Add("ADAPTER " + AnimationUtility.CalculateTransformPath(rect, _root) + " : " + component.GetType().Name); break;
                default: throw new FormatException("Blocked unsupported component " + component.GetType().FullName + " at " + rect.name);
            }
            if (data != null) { node.Add(data); Report.Add("CONVERTED " + AnimationUtility.CalculateTransformPath(rect, _root) + " : " + data.Name); }
        }
        foreach (RectTransform child in rect) { var value = Serialize(child, excluded); if (value != null) node.Add(value); }
        return node;
    }

    private static void BindSound(XElement root, UI_SoundSettingSlot row)
    {
        string group = row.Group.ToString().ToLowerInvariant(); var slider = Reference<Slider>(row, "_slider"); var toggle = Reference<Toggle>(row, "_muteToggle");
        var data = Find(root, slider.transform).Element("Slider"); A(data, "bind", "settings." + group + ".volume"); A(data, "command", "settings." + group + ".setVolume"); A(data, "min", 0); A(data, "max", 100); A(data, "wholeNumbers", true);
        data = Find(root, toggle.transform).Element("Toggle"); A(data, "bind", "settings." + group + ".enabled"); A(data, "command", "settings." + group + ".setEnabled"); Ref(data, "on", Reference<GameObject>(row, "_objSoundOn")); Ref(data, "off", Reference<GameObject>(row, "_objSoundOff"));
    }
    private static void BindSwitch(XElement root, UI_SlidingToggle control, string state, string command)
    { if (control == null) return; var component = Find(root, control.transform).Element("Switch"); A(component, "bind", state); A(component, "command", command); }
    private static void BindButton(XElement root, Button button, string command, string key, bool ingame = false)
    { if (button == null) return; var node = Find(root, button.transform); A(node, "key", key); A(node.Element("Button"), "command", command); if (ingame) A(node, "visible", "context.ingame"); }
    private static XElement Find(XElement root, Transform transform) => transform != null && _ids.TryGetValue(transform, out var id) ? root.DescendantsAndSelf("Element").FirstOrDefault(x => (string)x.Attribute("id") == id) : null;
    private static T Reference<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object => new SerializedObject(target).FindProperty(field)?.objectReferenceValue as T;
    private static void Ref(XElement node, string key, UnityEngine.Object reference)
    { Transform transform = reference == null ? null : reference is GameObject go ? go.transform : reference is Component component && component != null ? component.transform : null; A(node, key, transform != null && _ids.TryGetValue(transform, out string id) ? id : ""); }
    private static void Selectable(XElement node, Selectable control)
    { Ref(node, "target", control.targetGraphic); A(node, "interactable", control.interactable); if (node.Name == "Button") { A(node, "transition", (int)control.transition); A(node, "normal", C(control.colors.normalColor)); A(node, "highlighted", C(control.colors.highlightedColor)); A(node, "pressed", C(control.colors.pressedColor)); A(node, "selected", C(control.colors.selectedColor)); A(node, "disabled", C(control.colors.disabledColor)); } }
    private static void Diff(XElement template, XElement actual, XElement instance)
    {
        var sourceNodes = template.DescendantsAndSelf("Element").ToArray(); var actualNodes = actual.DescendantsAndSelf("Element").ToArray();
        if (sourceNodes.Length != actualNodes.Length) throw new FormatException("Sound rows have different structures; explicit migration required");
        var remap = actualNodes.Select((x, i) => new { Old = (string)x.Attribute("id"), New = (string)sourceNodes[i].Attribute("id") }).ToDictionary(x => x.Old, x => x.New);
        for (int i = 0; i < sourceNodes.Length; i++)
        {
            var target = sourceNodes[i]; var compared = new XElement(actualNodes[i]);
            foreach (var attribute in compared.DescendantsAndSelf().Attributes()) if (remap.TryGetValue(attribute.Value, out string id)) attribute.Value = id;
            foreach (var attribute in compared.Attributes().Where(x => x.Name != "id")) AddDiff("" + attribute.Name, attribute.Value, (string)target.Attribute(attribute.Name));
            foreach (var component in compared.Elements().Where(x => x.Name != "Element"))
                foreach (var attribute in component.Attributes())
                    if (attribute.Name != "bind" && attribute.Name != "command") AddDiff(component.Name + "." + attribute.Name, attribute.Value, (string)target.Element(component.Name)?.Attribute(attribute.Name));
            void AddDiff(string property, string value, string baseline)
            { if (value != baseline) instance.Add(new XElement("Override", new XAttribute("target", AuthoringWorkspace.Required(target, "id")), new XAttribute("property", property), new XAttribute("value", value))); }
        }
    }
    private static XElement Catalog()
    {
        var catalog = new XElement("Catalog", new XAttribute("version", "2"));
        foreach (string group in new[] { "master", "bgm", "sfx" })
        {
            State("settings." + group + ".volume", "number", "80", "0", "100"); State("settings." + group + ".enabled", "bool", "true");
            Command("settings." + group + ".setVolume", "number"); Command("settings." + group + ".setEnabled", "bool");
        }
        State("settings.damageNumbers", "bool", "true"); State("settings.vibration", "bool", "true"); State("settings.frameRate", "number", "30");
        State("settings.frame.canPrevious", "bool", "false"); State("settings.frame.canNext", "bool", "true"); State("context.ingame", "bool", "false");
        Command("settings.setDamageNumbers", "bool"); Command("settings.setVibration", "bool");
        foreach (string key in new[] { "settings.close", "settings.restart", "settings.lobby", "settings.frame.previous", "settings.frame.next" }) Command(key, "none");
        return catalog;
        void State(string key, string type, string value, string min = null, string max = null) { var item = new XElement("State", new XAttribute("key", key), new XAttribute("type", type), new XAttribute("default", value)); if (min != null) item.Add(new XAttribute("min", min), new XAttribute("max", max)); catalog.Add(item); }
        void Command(string key, string type) => catalog.Add(new XElement("Command", new XAttribute("key", key), new XAttribute("type", type)));
    }
    private static string SpriteKey(Sprite sprite)
    { if (sprite == null) return ""; if (Sprites.TryGetValue(sprite.name, out var other) && other != sprite) throw new FormatException("Resource key collision " + sprite.name); Sprites[sprite.name] = sprite; return sprite.name; }
    private static XElement ExportImage(string key, Sprite sprite)
    {
        string file = "Images/" + Identity(AssetDatabase.GetAssetPath(sprite) + key) + ".png";
        RenderTexture previous = RenderTexture.active;
        var render = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var readable = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height, TextureFormat.RGBA32, false, false);
        try { Graphics.Blit(sprite.texture, render); RenderTexture.active = render; readable.ReadPixels(sprite.rect, 0, 0); readable.Apply(); File.WriteAllBytes(Destination + "/" + file, readable.EncodeToPNG()); }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); UnityEngine.Object.DestroyImmediate(readable); }
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId);
        var node = new XElement("Image", new XAttribute("key", key), new XAttribute("file", file), new XAttribute("guid", guid), new XAttribute("localId", localId));
        const string artRoot = "Assets/Imports/GGD_ArtWork/";
        string assetPath = AssetDatabase.GetAssetPath(sprite);
        if (assetPath.StartsWith(artRoot, StringComparison.Ordinal)) node.SetAttributeValue("artPath", assetPath.Substring(artRoot.Length));
        A(node, "left", sprite.border.x); A(node, "bottom", sprite.border.y); A(node, "right", sprite.border.z); A(node, "top", sprite.border.w); A(node, "ppu", sprite.pixelsPerUnit); A(node, "pivotX", sprite.pivot.x / sprite.rect.width); A(node, "pivotY", sprite.pivot.y / sprite.rect.height); A(node, "filter", (int)sprite.texture.filterMode); A(node, "sha256", AuthoringWorkspace.Hash(File.ReadAllBytes(Destination + "/" + file))); return node;
    }
    private static void CompareUse(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { foreach (var panel in root.GetComponentsInChildren<UI_SettingPanel_InGame>(true)) Report.Add("USE " + path + " | " + AnimationUtility.CalculateTransformPath(panel.transform, root.transform) + " | allow=" + panel.AllowOpening + " | lobbyButtonActive=" + Reference<Button>(panel, "btn_GoToLobby")?.gameObject.activeSelf + " | rect=" + ((RectTransform)panel.transform).sizeDelta); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static string Identity(string text) => AuthoringWorkspace.Hash(Encoding.UTF8.GetBytes(Source + "|" + text)).Substring(0, 32);
    private static string C(Color color) => "#" + ColorUtility.ToHtmlStringRGBA(color);
    private static void A(XElement node, string key, object value) => node.SetAttributeValue(key, value is float number ? number.ToString("R", CultureInfo.InvariantCulture) : value is bool boolean ? boolean ? "true" : "false" : Convert.ToString(value, CultureInfo.InvariantCulture));
    private static void Save(string path, XElement root) => File.WriteAllText(Destination + "/" + path, new XDocument(root).ToString() + "\n", new UTF8Encoding(false));
}
