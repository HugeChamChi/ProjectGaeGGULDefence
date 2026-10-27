using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using GGD.UI.Authoring;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Validates a designer publication and installs the exact same source into the existing game's settings hosts.</summary>
public sealed class SettingsAuthoringBridge : IPreprocessBuildWithReport
{
    private const string AssetPath = "Assets/WorkSpace/HSD/Resources/UiAuthoring/Settings.asset";
    /// <inheritdoc />
    public int callbackOrder => 20;
    /// <summary>Builds must use an explicitly installed, current validated package.</summary>
    public void OnPreprocessBuild(BuildReport report)
    {
        if (!File.Exists("Data/UI/Authoring/kit.xml")) return;
        var assets = AssetDatabase.LoadAssetAtPath<AuthoringAssets>(AssetPath);
        string pointerPath = "Data/UI/Authoring/Published/current.xml";
        if (assets == null || !File.Exists(pointerPath) || assets.ContentHash != (string)XDocument.Load(pointerPath).Root.Attribute("sha256"))
            throw new BuildFailedException("Authored settings publication is not installed. Run Tools/UI Authoring/Install Published Settings.");
    }
    /// <summary>Creates the first publication from the actual extracted screen and installs it.</summary>
    public static void InstallInitial()
    {
        var workspace = AuthoringWorkspace.Open("Data/UI/Authoring"); KitPublisher.Publish(workspace); Install("Data/UI/Authoring");
    }
    /// <summary>Developer-side bridge for the portable creator kit.</summary>
    [MenuItem("Tools/UI Authoring/Import Designer Kit...")]
    public static void ImportDialog()
    {
        string directory = EditorUtility.OpenFolderPanel("Import published designer kit", "", ""); if (string.IsNullOrEmpty(directory)) return;
        Install(directory);
    }
    /// <summary>Installs the current canonical publication without changing its source.</summary>
    [MenuItem("Tools/UI Authoring/Install Published Settings")]
    public static void InstallCurrent() => Install("Data/UI/Authoring");

    /// <summary>Verifies pointer, every immutable byte hash, schema, components and contracts before replacing the game asset.</summary>
    public static void Install(string directory)
    {
        directory = Path.GetFullPath(directory);
        var pointer = AuthoringWorkspace.Parse(File.ReadAllText(AuthoringWorkspace.SafePath(directory, "Published/current.xml")), "current.xml").Root;
        string version = AuthoringWorkspace.Required(pointer, "version");
        string root = AuthoringWorkspace.SafePath(directory, "Published/versions/" + version);
        byte[] manifest = File.ReadAllBytes(Path.Combine(root, "manifest.xml"));
        if (AuthoringWorkspace.Hash(manifest) != (string)pointer.Attribute("sha256")) throw new FormatException("Publication manifest hash mismatch");
        foreach (var entry in AuthoringWorkspace.Parse(Encoding.UTF8.GetString(manifest), "manifest.xml").Root.Elements("File"))
        {
            byte[] bytes = File.ReadAllBytes(AuthoringWorkspace.SafePath(root, AuthoringWorkspace.Required(entry, "path")));
            if (bytes.Length != (long)entry.Attribute("bytes") || AuthoringWorkspace.Hash(bytes) != (string)entry.Attribute("sha256")) throw new FormatException("Publication changed: " + (string)entry.Attribute("path"));
        }
        var workspace = AuthoringWorkspace.Open(root);
        foreach (var doc in workspace.Documents.Where(x => x.Value.Root.Name == "Screen" || x.Value.Root.Name == "Prefab")) UiComposer.Resolve(workspace, doc.Key);
        var fontEntries = workspace.Documents["fonts.xml"].Root.Elements("Font").Select(x => new AuthoringAssets.FontEntry
        { Key = AuthoringWorkspace.Required(x, "key"), Asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AuthoringWorkspace.Required(x, "asset")) ?? throw new FormatException("Exact production font missing") }).ToArray();
        var imageEntries = workspace.Documents["images.xml"].Root.Elements("Image").Select(x => new AuthoringAssets.ImageEntry { Key = AuthoringWorkspace.Required(x, "key"), Asset = Image(x, root) }).ToArray();
        var textEntries = workspace.Documents.Select(x => new AuthoringAssets.TextEntry { Path = x.Key, Xml = AuthoringWorkspace.Serialize(x.Value) }).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)); AssetDatabase.Refresh();
        string contentHash = (string)pointer.Attribute("sha256");
        // A returned kit becomes the maintained original only after its complete package has validated.
        string canonical = Path.GetFullPath("Data/UI/Authoring");
        if (!string.Equals(directory, canonical, StringComparison.OrdinalIgnoreCase))
        {
            string backup = Path.GetFullPath("outputs/ui-authoring-import-backup/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")); Directory.CreateDirectory(backup);
            var previous = new Dictionary<string, byte[]>();
            string current = AuthoringWorkspace.SafePath(canonical, "Published/current.xml");
            previous[current] = File.Exists(current) ? File.ReadAllBytes(current) : null;
            try
            {
                foreach (var pair in workspace.Documents) Adopt(pair.Key, Encoding.UTF8.GetBytes(AuthoringWorkspace.Serialize(pair.Value)));
                foreach (var entry in workspace.Documents["images.xml"].Root.Elements("Image"))
                { string path = AuthoringWorkspace.Required(entry, "file"); Adopt(path, File.ReadAllBytes(AuthoringWorkspace.SafePath(root, path))); }
                KitPublisher.Publish(AuthoringWorkspace.Open(canonical));
                contentHash = (string)XDocument.Load(current).Root.Attribute("sha256");
            }
            catch
            {
                foreach (var pair in previous)
                    if (pair.Value != null) AuthoringWorkspace.AtomicWrite(pair.Key, pair.Value);
                    else if (File.Exists(pair.Key)) File.Delete(pair.Key);
                throw;
            }
            void Adopt(string relative, byte[] bytes)
            {
                string destination = AuthoringWorkspace.SafePath(canonical, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination));
                previous[destination] = File.Exists(destination) ? File.ReadAllBytes(destination) : null;
                if (previous[destination] != null) { string prior = AuthoringWorkspace.SafePath(backup, relative); Directory.CreateDirectory(Path.GetDirectoryName(prior)); File.WriteAllBytes(prior, previous[destination]); }
                AuthoringWorkspace.AtomicWrite(destination, bytes);
            }
        }
        // Commit the installed game asset only after the returned originals and pointer are complete.
        var target = AssetDatabase.LoadAssetAtPath<AuthoringAssets>(AssetPath);
        if (target == null) { target = ScriptableObject.CreateInstance<AuthoringAssets>(); AssetDatabase.CreateAsset(target, AssetPath); }
        target.Fonts = fontEntries; target.Images = imageEntries; target.Documents = textEntries; target.ContentHash = contentHash; target.SourceVersion = version;
        EditorUtility.SetDirty(target); AssetDatabase.SaveAssets();
        Debug.Log("UI_AUTHORING_INSTALLED " + version);
    }

    private static Sprite Image(XElement item, string publication)
    {
        string file = AuthoringWorkspace.SafePath(publication, AuthoringWorkspace.Required(item, "file")); byte[] bytes = File.ReadAllBytes(file);
        string guid = (string)item.Attribute("guid"), hash = (string)item.Attribute("sha256");
        Vector4 border = new Vector4(N("left"), N("bottom"), N("right"), N("top"));
        Vector2 pivot = new Vector2(N("pivotX", .5f), N("pivotY", .5f)); float ppu = N("ppu", 100);
        if (!string.IsNullOrEmpty(guid) && hash == AuthoringWorkspace.Hash(bytes))
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid); long localId = (long?)item.Attribute("localId") ?? 0;
            var installed = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault(x => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out string _, out long id); return id == localId; });
            if (installed != null && installed.border == border && installed.pixelsPerUnit == ppu && Vector2.Distance(installed.pivot / installed.rect.size, pivot) < .001f) return installed;
        }
        string generated = "Assets/WorkSpace/HSD/Resources/UiAuthoring/Generated/" + AuthoringWorkspace.Hash(bytes).Substring(0, 20) + "-" + AuthoringWorkspace.Hash(Encoding.UTF8.GetBytes(item.ToString())).Substring(0, 8) + Path.GetExtension(file);
        Directory.CreateDirectory(Path.GetDirectoryName(generated)); File.WriteAllBytes(generated, bytes); AssetDatabase.ImportAsset(generated, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(generated); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = ppu;
        importer.spriteBorder = border; importer.spritePivot = pivot;
        var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings); textureSettings.spriteAlignment = (int)SpriteAlignment.Custom; importer.SetTextureSettings(textureSettings);
        importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.filterMode = (FilterMode)(int)N("filter", 1); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(generated) ?? throw new FormatException("New image import failed: " + file);
        float N(string key, float fallback = 0) => AuthoringWorkspace.Number(item, key, fallback);
    }
}
