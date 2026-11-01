using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Portable lab experiments and file fonts, isolated from publishable game documents.</summary>
    public sealed class UiLabWorkspace : IDisposable
    {
        private readonly Dictionary<string, TMP_FontAsset> _fonts = new Dictionary<string, TMP_FontAsset>();
        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly HashSet<string> _usedMaterials = new HashSet<string>();
        private readonly Stack<string> _undo = new Stack<string>(), _redo = new Stack<string>();
        private XDocument _catalog;
        private string _saved, _disk;
        private bool _liveGroup, _liveRemembered;
        /// <summary>Portable lab root.</summary>
        public string DirectoryPath { get; }
        /// <summary>Current experiment original. Transient playback never writes here.</summary>
        public XDocument Document { get; private set; } = new XDocument(new XElement("Experiment", new XAttribute("schema", 1)));
        /// <summary>Registered font keys and portable files.</summary>
        public IEnumerable<XElement> Fonts => _catalog.Root.Elements("Font");
        /// <summary>Whether the current experiment differs from its saved original.</summary>
        public bool IsDirty => Document.ToString() != _saved;
        /// <summary>Constructs a portable workspace and restores its last experiment.</summary>
        public UiLabWorkspace(string directory)
        {
            DirectoryPath = Path.GetFullPath(directory); Directory.CreateDirectory(DirectoryPath);
            string path = Path.Combine(DirectoryPath, "fonts.xml");
            _catalog = File.Exists(path) ? AuthoringWorkspace.Parse(File.ReadAllText(path), path) : new XDocument(new XElement("Fonts"));
            if (File.Exists(PresetPath)) Load(); else _saved = Document.ToString();
        }
        private string PresetPath => Path.Combine(DirectoryPath, "experiment.xml");
        /// <summary>Registers a validated copy, without OS font installation or overwriting an existing font.</summary>
        public string RegisterFont(string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension != ".ttf" && extension != ".otf") throw new FormatException("TTF 또는 OTF 파일을 선택하세요.");
            byte[] bytes = File.ReadAllBytes(file); if (bytes.Length == 0 || bytes.Length > 64 * 1024 * 1024) throw new FormatException("폰트 크기는 0 초과 64MiB 이하여야 합니다.");
            string hash = AuthoringWorkspace.Hash(bytes), key = "font-" + hash.Substring(0, 20);
            var entry = Fonts.FirstOrDefault(x => (string)x.Attribute("key") == key);
            if (entry != null) { Font(key); return key; }
            string relative = "Fonts/" + hash + extension, destination = AuthoringWorkspace.SafePath(DirectoryPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); AuthoringWorkspace.AtomicWrite(destination, bytes);
            entry = new XElement("Font", new XAttribute("key", key), new XAttribute("name", Path.GetFileNameWithoutExtension(file)), new XAttribute("file", relative), new XAttribute("sha256", hash));
            _catalog.Root.Add(entry);
            try { Font(key); AuthoringWorkspace.AtomicWrite(Path.Combine(DirectoryPath, "fonts.xml"), System.Text.Encoding.UTF8.GetBytes(_catalog.ToString())); }
            catch { entry.Remove(); if (_fonts.Remove(key, out var font)) DestroyFont(font); throw; }
            return key;
        }
        /// <summary>Resolves a verified local font file and owns its dynamic TMP resources.</summary>
        public TMP_FontAsset Font(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_fonts.TryGetValue(key, out var existing)) return existing;
            var entry = Fonts.SingleOrDefault(x => (string)x.Attribute("key") == key) ?? throw new FormatException("등록되지 않은 폰트: " + key);
            string path = AuthoringWorkspace.SafePath(DirectoryPath, AuthoringWorkspace.Required(entry, "file"));
            if (!File.Exists(path) || AuthoringWorkspace.Hash(File.ReadAllBytes(path)) != (string)entry.Attribute("sha256")) throw new FormatException("폰트가 없거나 변경됐습니다: " + (string)entry.Attribute("name"));
            var font = TMP_FontAsset.CreateFontAsset(path, 0, 90, 12, GlyphRenderMode.SDFAA, 1024, 1024);
            if (font == null) throw new FormatException("폰트 파일을 읽을 수 없습니다: " + path);
            font.name = (string)entry.Attribute("name"); font.isMultiAtlasTexturesEnabled = true; _fonts.Add(key, font); return font;
        }
        /// <summary>Changes one style scope as one undoable transaction.</summary>
        public void Edit(string screen, string scope, IDictionary<string, string> values)
        {
            var node = Document.Root.Elements("Style").FirstOrDefault(x => (string)x.Attribute("screen") == screen && (string)x.Attribute("scope") == scope);
            if (node != null && values.All(x => (string)node.Attribute(x.Key) == x.Value)) return;
            Remember();
            if (node == null) { node = new XElement("Style", new XAttribute("screen", screen), new XAttribute("scope", scope)); Document.Root.Add(node); }
            foreach (var pair in values) node.SetAttributeValue(pair.Key, pair.Value);
        }
        /// <summary>Restores a selected scope to inherited/original appearance.</summary>
        public void Reset(string screen, string scope) { Remember(); Document.Root.Elements("Style").Where(x => (string)x.Attribute("screen") == screen && (string)x.Attribute("scope") == scope).Remove(); }
        /// <summary>Stores animation settings separately from display values.</summary>
        public void SetMotion(string screen, IDictionary<string, string> values)
        {
            var node = Document.Root.Elements("Motion").FirstOrDefault(x => (string)x.Attribute("screen") == screen);
            if (node != null && values.All(x => (string)node.Attribute(x.Key) == x.Value)) return;
            Remember();
            if (node == null) { node = new XElement("Motion", new XAttribute("screen", screen)); Document.Root.Add(node); }
            foreach (var pair in values) node.SetAttributeValue(pair.Key, pair.Value);
        }
        /// <summary>Returns the saved editable motion profile.</summary>
        public XElement Motion(string screen) => Document.Root.Elements("Motion").FirstOrDefault(x => (string)x.Attribute("screen") == screen);
        /// <summary>Groups consecutive live edits into one undo operation until explicitly ended.</summary>
        public void BeginLiveGroup() { if (_liveGroup) return; _liveGroup = true; _liveRemembered = false; }
        /// <summary>Ends a continuous slider or typing gesture.</summary>
        public void EndLiveGroup() { _liveGroup = false; _liveRemembered = false; }
        /// <summary>Finds a registered font identity without loading any additional fonts.</summary>
        public string FontKey(TMP_FontAsset font) => _fonts.FirstOrDefault(x => x.Value == font).Key ?? "";
        private void Remember() { if (_liveGroup && _liveRemembered) return; _undo.Push(Document.ToString()); _redo.Clear(); if (_liveGroup) _liveRemembered = true; }
        /// <summary>Reverts one experiment edit.</summary>
        public void Undo() { EndLiveGroup(); if (_undo.Count == 0) return; _redo.Push(Document.ToString()); Document = AuthoringWorkspace.Parse(_undo.Pop(), "undo"); }
        /// <summary>Reapplies one experiment edit.</summary>
        public void Redo() { EndLiveGroup(); if (_redo.Count == 0) return; _undo.Push(Document.ToString()); Document = AuthoringWorkspace.Parse(_redo.Pop(), "redo"); }
        /// <summary>Loads a schema-checked experiment without discarding the current one on failure.</summary>
        public void Load()
        {
            string text = File.ReadAllText(PresetPath); var candidate = AuthoringWorkspace.Parse(text, PresetPath);
            if (candidate.Root.Name != "Experiment" || (string)candidate.Root.Attribute("schema") != "1") throw new FormatException("지원하지 않는 실험 파일입니다.");
            foreach (var font in candidate.Descendants("Style").Attributes("font")) if (font.Value != "") Font(font.Value);
            Document = candidate; _disk = text; _saved = Document.ToString(); _undo.Clear(); _redo.Clear();
        }
        /// <summary>Saves with optimistic conflict detection and atomic file replacement.</summary>
        public void Save()
        {
            if (File.Exists(PresetPath) && File.ReadAllText(PresetPath) != _disk) throw new IOException("다른 창에서 실험 파일을 변경했습니다. 현재 값을 보존한 뒤 다시 여세요.");
            string text = Document.ToString(); AuthoringWorkspace.AtomicWrite(PresetPath, System.Text.Encoding.UTF8.GetBytes(text)); _disk = text; _saved = text;
        }
        /// <summary>Applies shared-role, screen-role and element overrides in precedence order.</summary>
        public void Apply(UiLabTemplate template, XDocument source = null)
        {
            _usedMaterials.Clear();
            source ??= Document;
            for (int i = 0; i < template.Rects.Length; i++)
            {
                var rect = template.Rects[i]; var text = rect.GetComponent<TMP_Text>(); string role = Role(text);
                var values = new Dictionary<string, string>();
                foreach (string screen in new[] { "*", template.Key })
                    foreach (string scope in new[] { "all", "role:" + role, template.Ids[i] })
                        foreach (var style in source.Root.Elements("Style").Where(x => (string)x.Attribute("screen") == screen && (string)x.Attribute("scope") == scope))
                            foreach (var value in style.Attributes().Where(x => x.Name != "scope" && x.Name != "screen")) values[value.Name.LocalName] = value.Value;
                if (text != null)
                {
                    if (text.font == null) text.font = TMP_Settings.defaultFontAsset;
                    if (text.fontSharedMaterial == null && text.font != null) text.fontSharedMaterial = text.font.material;
                    if (text.fontSharedMaterial == null) throw new FormatException("화면 글자의 기본 폰트가 없습니다: " + rect.name);
                    float originalOutline = text.fontSharedMaterial.GetFloat("_OutlineWidth"), originalSoftness = text.fontSharedMaterial.GetFloat("_OutlineSoftness");
                    Color originalEdge = text.fontSharedMaterial.GetColor("_OutlineColor"); bool changedFont = false;
                    if (values.TryGetValue("font", out string key) && key != "") { var font = Font(key); changedFont = text.font != font; text.font = font; }
                    if (values.TryGetValue("text", out string copy)) text.text = copy;
                    if (values.TryGetValue("size", out string size)) text.fontSize = Number(size);
                    if (values.TryGetValue("color", out string color)) text.color = ParseColor(color);
                    if (values.TryGetValue("spacing", out string spacing)) text.characterSpacing = Number(spacing);
                    if (values.TryGetValue("line", out string line)) text.lineSpacing = Number(line);
                    if (values.TryGetValue("auto", out string auto)) text.enableAutoSizing = bool.Parse(auto);
                    float width = values.TryGetValue("outline", out string outline) ? Number(outline) : originalOutline;
                    Color edge = values.TryGetValue("edge", out string edgeColor) ? ParseColor(edgeColor) : originalEdge;
                    float soft = values.TryGetValue("soft", out string softness) ? Number(softness) : originalSoftness;
                    if (changedFont || values.ContainsKey("outline") || values.ContainsKey("edge") || values.ContainsKey("soft"))
                    {
                        string materialKey = text.fontSharedMaterial.GetInstanceID() + "|" + width + "|" + edge + "|" + soft;
                        if (!_materials.TryGetValue(materialKey, out var material))
                        {
                            material = new Material(text.fontSharedMaterial) { name = "Lab outline" }; material.SetFloat("_OutlineWidth", width); material.SetColor("_OutlineColor", edge); material.SetFloat("_OutlineSoftness", soft); _materials.Add(materialKey, material);
                        }
                        _usedMaterials.Add(materialKey);
                        text.fontSharedMaterial = material;
                    }
                    text.UpdateMeshPadding();
                }
                var image = rect.GetComponent<UnityEngine.UI.Image>();
                if (image != null && values.TryGetValue("tint", out string tint)) image.color = ParseColor(tint);
                if (values.TryGetValue("dx", out string dx)) rect.anchoredPosition += new Vector2(Number(dx), 0);
                if (values.TryGetValue("dy", out string dy)) rect.anchoredPosition += new Vector2(0, Number(dy));
                if (values.TryGetValue("width", out string w)) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Number(w));
                if (values.TryGetValue("height", out string h)) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Number(h));
            }
        }
        /// <summary>Classifies a captured text consistently without changing its name or gameplay data.</summary>
        public static string Role(TMP_Text text)
        {
            if (text == null) return "image";
            string name = text.name.ToLowerInvariant();
            if (name.Contains("name") || name.Contains("title")) return "title";
            if (name.Contains("cost") || name.Contains("level") || name.Contains("value") || name.Contains("timer")) return "number";
            if (text.GetComponentInParent<UnityEngine.UI.Button>() != null) return "button";
            return "body";
        }
        /// <summary>Parses finite numeric editor values.</summary>
        public static float Number(string value) { float number = float.Parse(value, CultureInfo.InvariantCulture); if (float.IsNaN(number) || float.IsInfinity(number)) throw new FormatException("유한한 숫자를 입력하세요."); return number; }
        /// <summary>Parses a hex color with an explicit failure.</summary>
        public static Color ParseColor(string value) => ColorUtility.TryParseHtmlString(value, out var color) ? color : throw new FormatException("색상은 #RRGGBB 또는 #RRGGBBAA 형식입니다.");
        /// <summary>Releases temporary outline combinations after the previous visual was replaced.</summary>
        public void ClearMaterials() { foreach (var material in _materials.Values) UnityEngine.Object.Destroy(material); _materials.Clear(); }
        /// <summary>Releases combinations no longer referenced after a successful visual replacement.</summary>
        public void ReleaseUnusedMaterials()
        {
            foreach (string key in _materials.Keys.Where(x => !_usedMaterials.Contains(x)).ToArray()) { UnityEngine.Object.Destroy(_materials[key]); _materials.Remove(key); }
        }
        private static void DestroyFont(TMP_FontAsset font) { foreach (var atlas in font.atlasTextures) if (atlas != null) UnityEngine.Object.Destroy(atlas); if (font.material != null) UnityEngine.Object.Destroy(font.material); UnityEngine.Object.Destroy(font); }
        /// <summary>Releases only lab-owned font and material resources.</summary>
        public void Dispose() { ClearMaterials(); foreach (var font in _fonts.Values) DestroyFont(font); _fonts.Clear(); }
    }
}
