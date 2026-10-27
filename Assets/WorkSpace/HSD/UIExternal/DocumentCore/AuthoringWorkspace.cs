using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GGD.UI.Authoring
{
    /// <summary>Engine-independent original documents, atomic persistence and undoable editing.</summary>
    public sealed class AuthoringWorkspace
    {
        private readonly Dictionary<string, XDocument> _documents = new Dictionary<string, XDocument>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _diskHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Stack<Dictionary<string, string>> _undo = new Stack<Dictionary<string, string>>();
        private readonly Stack<Dictionary<string, string>> _redo = new Stack<Dictionary<string, string>>();
        private string _savedFingerprint;
        /// <summary>Directory containing the complete portable kit.</summary>
        public string DirectoryPath { get; private set; }
        /// <summary>Source documents indexed by kit-relative path; resolved trees are always separate copies.</summary>
        public IReadOnlyDictionary<string, XDocument> Documents => _documents;
        /// <summary>Contract and resource catalog supplied by the developer.</summary>
        public XDocument Catalog => _documents["catalog.xml"];
        /// <summary>Current writable screen or prefab document.</summary>
        public string CurrentPath { get; set; } = "Screens/UI3030.xml";
        /// <summary>Whether memory differs from the last successful disk save.</summary>
        public bool IsDirty => Fingerprint() != _savedFingerprint;
        /// <summary>Unknown future schemas can be inspected but never rewritten.</summary>
        public bool IsReadOnly { get; private set; }
        /// <summary>Available undo operations.</summary>
        public int UndoCount => _undo.Count;
        /// <summary>Available redo operations.</summary>
        public int RedoCount => _redo.Count;

        /// <summary>Reads installed XML snapshots entirely in memory; game builds never load an external authoring directory.</summary>
        public static AuthoringWorkspace FromSources(IReadOnlyDictionary<string, string> sources)
        {
            var workspace = new AuthoringWorkspace();
            foreach (var pair in sources) workspace._documents[pair.Key] = Parse(pair.Value, pair.Key);
            var root = workspace._documents["kit.xml"].Root;
            if ((string)root.Attribute("project") != "GaeGGULDefence" || (string)root.Attribute("schema") != "2" || (string)root.Attribute("runtime") != "2")
                throw new FormatException("Incompatible installed UI kit");
            workspace._savedFingerprint = workspace.Fingerprint(); return workspace;
        }

        /// <summary>Opens a standalone kit. No Unity project or absolute project paths are required.</summary>
        public static AuthoringWorkspace Open(string directory)
        {
            var workspace = new AuthoringWorkspace { DirectoryPath = Path.GetFullPath(directory) };
            var kit = Parse(File.ReadAllText(SafePath(directory, "kit.xml")), "kit.xml");
            if ((string)kit.Root.Attribute("project") != "GaeGGULDefence") throw new FormatException("This kit belongs to a different project");
            workspace.IsReadOnly = (string)kit.Root.Attribute("schema") != "2" || (string)kit.Root.Attribute("runtime") != "2";
            foreach (string path in new[] { "kit.xml", "catalog.xml", "images.xml", "fonts.xml" }
                         .Concat(kit.Root.Elements("Document").Select(x => Required(x, "path"))))
            {
                if (workspace._documents.ContainsKey(path)) throw new FormatException("Duplicate document " + path);
                string text = File.ReadAllText(SafePath(directory, path));
                workspace._documents.Add(path, Parse(text, path)); workspace._diskHashes[path] = Hash(Encoding.UTF8.GetBytes(text));
            }
            workspace._savedFingerprint = workspace.Fingerprint();
            return workspace;
        }

        /// <summary>Performs one atomic in-memory action, including actions spanning multiple documents.</summary>
        public void Edit(Action mutation)
        {
            if (IsReadOnly) throw new InvalidOperationException("Newer kit schema: read-only");
            var before = Snapshot();
            try { mutation(); }
            catch { Restore(before); throw; }
            if (Equivalent(before, Snapshot())) return;
            _undo.Push(before); _redo.Clear();
        }

        /// <summary>Captures a gesture start so a full drag is one undo operation.</summary>
        public Dictionary<string, string> Snapshot() => _documents.ToDictionary(x => x.Key, x => x.Value.ToString(SaveOptions.DisableFormatting), StringComparer.Ordinal);
        /// <summary>Commits a gesture whose live updates have already changed the source.</summary>
        public void CommitGesture(Dictionary<string, string> before)
        {
            if (IsReadOnly) { Restore(before); throw new InvalidOperationException("Read-only kit"); }
            if (!Equivalent(before, Snapshot())) { _undo.Push(before); _redo.Clear(); }
        }
        /// <summary>Undoes one source transaction.</summary>
        public void Undo() { if (_undo.Count == 0) return; _redo.Push(Snapshot()); Restore(_undo.Pop()); }
        /// <summary>Redoes one source transaction.</summary>
        public void Redo() { if (_redo.Count == 0) return; _undo.Push(Snapshot()); Restore(_redo.Pop()); }
        /// <summary>Finds an original element by stable identity.</summary>
        public XElement Find(string path, string id) => _documents[path].Descendants().FirstOrDefault(x => (string)x.Attribute("id") == id);

        /// <summary>Adds a source document and updates the kit index as part of the caller's undo transaction.</summary>
        public void AddDocument(string path, XDocument document)
        {
            SafePath(DirectoryPath, path);
            if (_documents.ContainsKey(path) || File.Exists(SafePath(DirectoryPath, path))) throw new IOException("Document already exists " + path);
            _documents.Add(path, document);
            _documents["kit.xml"].Root.Add(new XElement("Document", new XAttribute("path", path)));
        }

        /// <summary>Renames one image key and its typed source/override/parameter usages in one undoable transaction.</summary>
        public void RenameImage(string oldKey, string newKey)
        {
            if (string.IsNullOrWhiteSpace(newKey)) throw new ArgumentException("Image key is empty");
            var images = _documents["images.xml"].Root;
            if (images.Elements("Image").Any(x => (string)x.Attribute("key") == newKey)) throw new ArgumentException("Image key already exists");
            var image = images.Elements("Image").Single(x => (string)x.Attribute("key") == oldKey);
            Edit(() =>
            {
                image.SetAttributeValue("key", newKey);
                var parameters = new Dictionary<string, HashSet<string>>();
                foreach (var document in _documents.Values.Where(x => x.Root.Name == "Prefab"))
                {
                    var keys = new HashSet<string>(document.Root.Element("Parameters")?.Elements("Parameter").Where(x => document.Descendants("Image").Any(i => (string)i.Attribute("sprite") == "${" + (string)x.Attribute("key") + "}")).Select(x => (string)x.Attribute("key")) ?? Enumerable.Empty<string>());
                    parameters[(string)document.Root.Attribute("key")] = keys;
                    foreach (var parameter in document.Root.Element("Parameters")?.Elements("Parameter").Where(x => keys.Contains((string)x.Attribute("key")) && (string)x.Attribute("default") == oldKey) ?? Enumerable.Empty<XElement>()) parameter.SetAttributeValue("default", newKey);
                }
                foreach (var document in _documents.Values)
                {
                    foreach (var node in document.Descendants("Image").Where(x => (string)x.Attribute("sprite") == oldKey)) node.SetAttributeValue("sprite", newKey);
                    foreach (var node in document.Descendants("Override").Where(x => (string)x.Attribute("property") == "Image.sprite" && (string)x.Attribute("value") == oldKey)) node.SetAttributeValue("value", newKey);
                    foreach (var instance in document.Descendants("Instance"))
                        if (parameters.TryGetValue((string)instance.Attribute("prefab"), out var keys))
                            foreach (var input in instance.Elements("Input").Where(x => keys.Contains((string)x.Attribute("key")) && (string)x.Attribute("value") == oldKey)) input.SetAttributeValue("value", newKey);
                }
            });
        }

        /// <summary>Changes an original property or a prefab override without flattening inherited data.</summary>
        public void SetProperty(string resolvedId, string property, string value)
        {
            string[] segments = resolvedId.Split('/');
            if (segments.Length == 1) SetValue(Find(CurrentPath, resolvedId) ?? throw new FormatException("Element no longer exists"), property, value);
            else
            {
                var instance = Find(CurrentPath, segments[0]) ?? throw new FormatException("Instance no longer exists");
                string target = string.Join("/", segments.Skip(1));
                var item = instance.Elements("Override").FirstOrDefault(x => (string)x.Attribute("target") == target && (string)x.Attribute("property") == property);
                if (item == null) { item = new XElement("Override", new XAttribute("target", target), new XAttribute("property", property)); instance.Add(item); }
                item.SetAttributeValue("value", value);
            }
        }

        /// <summary>Returns a property on the source element or component.</summary>
        public static string Value(XElement element, string property)
        {
            string[] parts = property.Split('.');
            return parts.Length == 1 ? (string)element.Attribute(property) : (string)element.Element(parts[0])?.Attribute(parts[1]);
        }
        /// <summary>Sets one typed renderer attribute; a component must exist before editing its attributes.</summary>
        public static void SetValue(XElement element, string property, string value)
        {
            string[] parts = property.Split('.');
            if (parts.Length == 1) element.SetAttributeValue(property, value);
            else (element.Element(parts[0]) ?? throw new FormatException("Missing component " + parts[0])).SetAttributeValue(parts[1], value);
        }

        /// <summary>Saves every source atomically with conflict detection, backups and rollback on multi-file failure.</summary>
        public void Save(bool overwriteConflict = false)
        {
            if (IsReadOnly) throw new InvalidOperationException("Newer schema: cannot save");
            foreach (var pair in _diskHashes)
            {
                string path = SafePath(DirectoryPath, pair.Key);
                if (!overwriteConflict && (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != pair.Value))
                    throw new IOException("File conflict: " + pair.Key + ". Reopen or explicitly overwrite after comparison.");
            }
            var saved = new Dictionary<string, byte[]>();
            foreach (string key in _documents.Keys.Where(x => !_diskHashes.ContainsKey(x)))
                if (File.Exists(SafePath(DirectoryPath, key))) throw new IOException("New file conflict: " + key);
            try
            {
                // Only files loaded or saved by this workspace are owned. External files are never swept.
                foreach (string key in _diskHashes.Keys.Where(x => !_documents.ContainsKey(x)))
                {
                    string path = SafePath(DirectoryPath, key);
                    // Removed documents must never overwrite an external edit, even with overwriteConflict.
                    if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != _diskHashes[key])
                        throw new IOException("Removed file conflict: " + key);
                    saved[path] = File.ReadAllBytes(path);
                    File.Delete(path);
                }
                foreach (var pair in _documents)
                {
                    string path = SafePath(DirectoryPath, pair.Key); Directory.CreateDirectory(Path.GetDirectoryName(path));
                    saved[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
                    AtomicWrite(path, Encoding.UTF8.GetBytes(Serialize(pair.Value)));
                }
            }
            catch
            {
                foreach (var pair in saved)
                    if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); }
                    else AtomicWrite(pair.Key, pair.Value);
                throw;
            }
            _diskHashes.Clear();
            foreach (string path in _documents.Keys) _diskHashes[path] = Hash(File.ReadAllBytes(SafePath(DirectoryPath, path)));
            _savedFingerprint = Fingerprint();
        }

        /// <summary>Writes an atomic recovery file containing all unsaved XML without replacing the originals.</summary>
        public void SaveRecovery() => AtomicWrite(SafePath(DirectoryPath, ".studio-recovery.xml"), Encoding.UTF8.GetBytes(new XElement("Recovery",
            Snapshot().Select(x => new XElement("Document", new XAttribute("path", x.Key), new XCData(x.Value)))).ToString()));

        /// <summary>Restores a recovery snapshot as an undoable memory operation, keeping disk originals intact.</summary>
        public void Recover()
        {
            var recovered = Parse(File.ReadAllText(SafePath(DirectoryPath, ".studio-recovery.xml")), "recovery");
            Edit(() => Restore(recovered.Root.Elements("Document").ToDictionary(x => Required(x, "path"), x => x.Value)));
        }

        /// <summary>Duplicates a subtree and remaps all internal identity references.</summary>
        public XElement Duplicate(XElement source)
        {
            var copy = new XElement(source); var mapping = new Dictionary<string, string>();
            foreach (var item in copy.DescendantsAndSelf().Where(x => x.Attribute("id") != null))
            { string old = Required(item, "id"); string id = Guid.NewGuid().ToString("N"); mapping[old] = id; item.SetAttributeValue("id", id); }
            foreach (var attribute in copy.DescendantsAndSelf().Attributes().Where(x => new[] { "fill", "handle", "target", "graphic", "on", "off", "visualSlider" }.Contains(x.Name.LocalName)))
                if (mapping.TryGetValue(attribute.Value, out string id)) attribute.Value = id;
            source.AddAfterSelf(copy); return copy;
        }

        /// <summary>Loads safe, bounded XML and retains filename/line information.</summary>
        public static XDocument Parse(string text, string name)
        {
            using var input = new StringReader(text);
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 }, name);
            try { return XDocument.Load(reader, LoadOptions.SetBaseUri | LoadOptions.SetLineInfo); }
            catch (XmlException error) { throw new FormatException($"{name}:{error.LineNumber}:{error.LinePosition}: {error.Message}"); }
        }
        /// <summary>Formats a stable UTF-8 source document.</summary>
        public static string Serialize(XDocument document) => document.ToString() + "\n";
        /// <summary>Returns SHA-256 for conflict and publication checks.</summary>
        public static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        /// <summary>Reads a mandatory, nonempty attribute.</summary>
        public static string Required(XElement node, string key) => !string.IsNullOrWhiteSpace((string)node.Attribute(key)) ? (string)node.Attribute(key) : throw new FormatException(node.BaseUri + ": missing " + key);
        /// <summary>Reads a finite invariant number.</summary>
        public static float Number(XElement node, string key, float fallback = 0)
        {
            string raw = (string)node.Attribute(key);
            if (raw == null) return fallback;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsInfinity(value) || float.IsNaN(value)) throw new FormatException("Invalid number " + key);
            return value;
        }
        /// <summary>Confines all kit files to regular paths below the selected directory.</summary>
        public static string SafePath(string root, string path)
        {
            string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (string.IsNullOrEmpty(path) || path.Contains("\\") || path.Contains(":") || Path.IsPathRooted(path) || path.Split('/').Any(x => x == "." || x == ".." || x.Length == 0)) throw new FormatException("Invalid kit path " + path);
            string full = Path.GetFullPath(Path.Combine(basePath, path));
            if (!full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new FormatException("Path outside kit");
            for (string p = full; p != null && p.Length >= basePath.Length - 1; p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || System.IO.Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new FormatException("Linked kit paths are not supported");
            return full;
        }
        /// <summary>Atomically writes one file on the same volume.</summary>
        public static void AtomicWrite(string path, byte[] bytes)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private void Restore(Dictionary<string, string> values)
        { _documents.Clear(); foreach (var pair in values) _documents[pair.Key] = Parse(pair.Value, pair.Key); }
        private string Fingerprint() => string.Join("\n", Snapshot().OrderBy(x => x.Key).Select(x => x.Key + x.Value));
        private static bool Equivalent(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && a.All(x => b.TryGetValue(x.Key, out string value) && value == x.Value);
    }
}
