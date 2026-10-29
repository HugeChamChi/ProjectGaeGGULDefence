using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DG.Tweening;
using GGD.UI.Authoring;
using HSD.UI.Setting;
using HSD.UI.Utils;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Exports the real settings hierarchy for portable visual editing and applies validated deltas to its original.</summary>
public static class NativeSettingsPrefabBridge
{
    /// <summary>Existing settings prefab shared by the game hosts.</summary>
    public const string Source = "Assets/WorkSpace/HSD/Prefab/UI/InGame/InGame_Setting_Popup.prefab";
    private const string Installed = "Assets/WorkSpace/HSD/Resources/UiAuthoring/Settings.asset";
    /// <summary>Creates an independent native editing kit without overwriting any existing authoring work.</summary>
    public static void Export(string directory, string sourceKit = "Data/UI/Authoring")
    {
        directory = Path.GetFullPath(directory);
        if (Directory.Exists(directory)) throw new IOException("Export destination already exists: " + directory);
        var workspace = AuthoringWorkspace.Open(sourceKit);
        var resolved = UiComposer.Resolve(workspace, "Screens/UI3030.xml");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        var previous = NativePrefabContract.Read(workspace);
        var installedMode = AssetDatabase.LoadAssetAtPath<AuthoringAssets>(Installed);
        if (previous == null && installedMode.NativeSourceInstalled) throw new InvalidOperationException("이미 원본 프리팹 모드를 적용했습니다. 적용한 키트로 Export Next Native Revision을 사용하세요.");
        if (previous != null)
        {
            var pointer = XDocument.Load(AuthoringWorkspace.SafePath(sourceKit, "Published/current.xml")).Root;
            string publication = AuthoringWorkspace.SafePath(sourceKit, "Published/versions/" + AuthoringWorkspace.Required(pointer, "version"));
            var published = AuthoringWorkspace.Open(publication);
            if (!installedMode.NativeSourceInstalled || installedMode.NativeSourceHash != AuthoringWorkspace.Hash(File.ReadAllBytes(Source)) || installedMode.SourceVersion != "native:" + (string)pointer.Attribute("sha256"))
                throw new InvalidOperationException("이 키트의 적용 상태와 Unity 원본이 다릅니다. 변경을 확인하고 병합한 후 내보내세요.");
            if (workspace.Snapshot().Count != published.Snapshot().Count || workspace.Snapshot().Any(x => !published.Snapshot().TryGetValue(x.Key, out var value) || value != x.Value))
                throw new InvalidOperationException("적용 뒤 키트가 수정되었습니다. 적용한 원본 키트로 다음 버전을 내보내세요.");
        }
        var mappings = previous == null ? Map(asset, resolved) : Remap(asset, previous);
        string sourceHash = AuthoringWorkspace.Hash(File.ReadAllBytes(Source));
        string generated = "Assets/WorkSpace/HSD/Generated/UiAuthoring/Native/" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(generated); AssetDatabase.Refresh();
        var root = PrefabUtility.LoadPrefabContents(Source);
        try
        {
            root.SetActive(false);
            var host = root.GetComponent<HSD.UI.Setting.UI_SettingPanel_InGame>();
            var hostData = new SerializedObject(host);
            var animation = hostData.FindProperty("targetAnim").objectReferenceValue as GaeGGUL.Animation.Anim_UI_Scale;
            if (animation == null) throw new InvalidOperationException("This native pilot requires the existing Anim_UI_Scale popup animation.");
            var profile = new SerializedObject(animation);
            var target = (Transform)profile.FindProperty("animationTarget").objectReferenceValue ?? animation.transform;
            var template = root.AddComponent<NativePrefabTemplate>();
            template.Ids = mappings.Select(x => (string)x.Attribute("id")).ToArray();
            template.Rects = mappings.Select(x => At(root.transform, (string)x.Attribute("path")) as RectTransform).ToArray();
            template.AnimationId = template.Ids[Array.IndexOf(template.Rects, target)];
            template.DurationIn = profile.FindProperty("durationIn").floatValue; template.DurationOut = profile.FindProperty("durationOut").floatValue;
            template.DelayIn = profile.FindProperty("delayIn").floatValue; template.DelayOut = profile.FindProperty("delayOut").floatValue;
            template.StartMultiplier = profile.FindProperty("startMultiplier").vector2Value; template.EndMultiplier = profile.FindProperty("endMultiplier").vector2Value;
            template.EaseIn = Curve((Ease)profile.FindProperty("easeIn").intValue); template.EaseOut = Curve((Ease)profile.FindProperty("easeOut").intValue);
            var confirm = hostData.FindProperty("confirmPopup").objectReferenceValue as Component;
            if (confirm != null) confirm.gameObject.SetActive(false);
            foreach (var button in root.GetComponentsInChildren<Button>(true)) while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
            foreach (var slider in root.GetComponentsInChildren<Slider>(true)) while (slider.onValueChanged.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(slider.onValueChanged, 0);
            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true)) while (toggle.onValueChanged.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(toggle.onValueChanged, 0);
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing source script");
                string space = component.GetType().Namespace ?? "";
                if (component == template || space == "UnityEngine.UI" || space == "TMPro") continue;
                UnityEngine.Object.DestroyImmediate(component);
            }
            string prefab = generated + "/SettingsNative.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
            Directory.CreateDirectory(directory);
            foreach (var pair in workspace.Documents.Where(x => x.Key != NativePrefabContract.Path)) Write(directory, pair.Key, Encoding.UTF8.GetBytes(AuthoringWorkspace.Serialize(pair.Value)));
            foreach (var entry in workspace.Documents["images.xml"].Root.Elements("Image"))
            { string path = (string)entry.Attribute("file"); Write(directory, path, File.ReadAllBytes(AuthoringWorkspace.SafePath(workspace.DirectoryPath, path))); }
            string bundles = Path.Combine(directory, "Native"); Directory.CreateDirectory(bundles);
            string bundleName = "settings-" + Guid.NewGuid().ToString("N") + ".bundle";
            var result = BuildPipeline.BuildAssetBundles(bundles, new[] { new AssetBundleBuild { assetBundleName = bundleName, assetNames = new[] { prefab } } }, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            if (result == null) throw new InvalidOperationException("Native preview bundle build failed");
            string relative = "Native/" + bundleName;
            var contract = new XElement("NativePrefab", new XAttribute("source", Source), new XAttribute("guid", AssetDatabase.AssetPathToGUID(Source)), new XAttribute("sourceHash", sourceHash),
                new XAttribute("bundle", relative), new XAttribute("bundleHash", AuthoringWorkspace.Hash(File.ReadAllBytes(Path.Combine(directory, relative)))),
                new XElement("Mappings", mappings), new XElement("Baseline", new XElement(resolved.Root)));
            Write(directory, NativePrefabContract.Path, Encoding.UTF8.GetBytes(contract.ToString()));
            var kit = XDocument.Load(Path.Combine(directory, "kit.xml")); kit.Root.Elements("Document").Where(x => (string)x.Attribute("path") == NativePrefabContract.Path).Remove();
            kit.Root.Add(new XElement("Document", new XAttribute("path", NativePrefabContract.Path))); Write(directory, "kit.xml", Encoding.UTF8.GetBytes(AuthoringWorkspace.Serialize(kit)));
            NativePrefabContract.Validate(AuthoringWorkspace.Open(directory), resolved);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Exports the first native settings kit to a newly selected directory.</summary>
    [MenuItem("Tools/UI Authoring/Export Native Settings Kit...")]
    public static void ExportDialog()
    {
        string parent = EditorUtility.OpenFolderPanel("원본 설정창 키트를 저장할 상위 폴더", "", "");
        if (!string.IsNullOrEmpty(parent)) Export(Path.Combine(parent, "NativeSettings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
    }
    /// <summary>Creates the next native revision from a returned kit after its edits have been applied.</summary>
    [MenuItem("Tools/UI Authoring/Export Next Native Revision...")]
    public static void ExportNextDialog()
    {
        string source = EditorUtility.OpenFolderPanel("적용한 원본 프리팹 키트", "", ""); if (string.IsNullOrEmpty(source)) return;
        Export(source.TrimEnd('/', '\\') + "-next-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), source);
    }

    /// <summary>Validates a publication and applies visual deltas; any save/install failure restores the original asset bytes.</summary>
    public static void Apply(string directory)
    {
        var pointer = XDocument.Load(AuthoringWorkspace.SafePath(directory, "Published/current.xml")).Root;
        string publication = AuthoringWorkspace.SafePath(directory, "Published/versions/" + AuthoringWorkspace.Required(pointer, "version"));
        byte[] manifest = File.ReadAllBytes(Path.Combine(publication, "manifest.xml"));
        if (AuthoringWorkspace.Hash(manifest) != (string)pointer.Attribute("sha256")) throw new FormatException("Manifest changed");
        foreach (var file in AuthoringWorkspace.Parse(Encoding.UTF8.GetString(manifest), "manifest").Root.Elements("File"))
        {
            byte[] bytes = File.ReadAllBytes(AuthoringWorkspace.SafePath(publication, AuthoringWorkspace.Required(file, "path")));
            if (bytes.LongLength != (long)file.Attribute("bytes") || AuthoringWorkspace.Hash(bytes) != (string)file.Attribute("sha256")) throw new FormatException("Publication changed");
        }
        var workspace = AuthoringWorkspace.Open(publication); var contract = NativePrefabContract.Read(workspace) ?? throw new FormatException("Native source missing");
        var resolved = UiComposer.Resolve(workspace, "Screens/UI3030.xml"); NativePrefabContract.Validate(workspace, resolved);
        if ((string)contract.Attribute("source") != Source || (string)contract.Attribute("guid") != AssetDatabase.AssetPathToGUID(Source)) throw new FormatException("Original prefab identity changed");
        byte[] original = File.ReadAllBytes(Source); byte[] installed = File.ReadAllBytes(Installed);
        if (AuthoringWorkspace.Hash(original) != (string)contract.Attribute("sourceHash")) throw new FormatException("Unity原本已變更。Unity 원본이 내보낸 뒤 변경되었습니다. 다시 내보내거나 변경을 먼저 병합하세요.");
        string backup = "outputs/ui-native-prefab-backups/" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(backup);
        File.WriteAllBytes(backup + "/Original.prefab", original); File.WriteAllBytes(backup + "/Settings.asset", installed);
        var root = PrefabUtility.LoadPrefabContents(Source);
        try
        {
            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            var baseline = contract.Element("Baseline").Element("Resolved").Descendants("Element").ToDictionary(x => (string)x.Attribute("id"));
            var current = resolved.Root.Descendants("Element").ToDictionary(x => (string)x.Attribute("id"));
            foreach (var map in contract.Element("Mappings").Elements("Map"))
            {
                string id = (string)map.Attribute("id"), path = (string)map.Attribute("path");
                var persistent = At(sourceAsset.transform, path);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(persistent, out string guid, out long fileId);
                if (fileId.ToString() != (string)map.Attribute("fileId")) throw new FormatException("Native object identity changed: " + id);
                ApplyDelta((RectTransform)At(root.transform, path), baseline[id], current[id], workspace, publication);
            }
            PrefabUtility.SaveAsPrefabAsset(root, Source);
            var settings = AssetDatabase.LoadAssetAtPath<AuthoringAssets>(Installed);
            settings.NativeSourceInstalled = true; settings.NativeSourceHash = AuthoringWorkspace.Hash(File.ReadAllBytes(Source));
            settings.SourceVersion = "native:" + (string)pointer.Attribute("sha256");
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }
        catch
        {
            File.WriteAllBytes(Source, original); File.WriteAllBytes(Installed, installed); AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(Installed, ImportAssetOptions.ForceUpdate); throw;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ApplyDelta(RectTransform rect, XElement before, XElement after, AuthoringWorkspace workspace, string publication)
    {
        // A resource may change under the same key. Resolve the published mapping even when the element's sprite key did not change.
        if (after.Element("Image") is XElement imageNode)
        {
            string key = (string)imageNode.Attribute("sprite");
            rect.GetComponent<Image>().sprite = string.IsNullOrEmpty(key) ? null : SettingsAuthoringBridge.ResolveImage(workspace.Documents["images.xml"].Root.Elements("Image").Single(x => (string)x.Attribute("key") == key), publication);
        }
        // Apply only changed fields. Normalized preview roots and unchanged driven rects never overwrite native layout.
        foreach (var value in after.Attributes().Where(a => (string)before.Attribute(a.Name) != a.Value))
        {
            string key = value.Name.LocalName; float N() => float.Parse(value.Value, System.Globalization.CultureInfo.InvariantCulture);
            switch (key)
            {
                case "name": rect.name = value.Value; break; case "active": rect.gameObject.SetActive(bool.Parse(value.Value)); break;
                case "x": rect.anchoredPosition = new Vector2(N(), rect.anchoredPosition.y); break; case "y": rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, N()); break;
                case "width": rect.sizeDelta = new Vector2(N(), rect.sizeDelta.y); break; case "height": rect.sizeDelta = new Vector2(rect.sizeDelta.x, N()); break;
                case "anchorMinX": rect.anchorMin = new Vector2(N(), rect.anchorMin.y); break; case "anchorMinY": rect.anchorMin = new Vector2(rect.anchorMin.x, N()); break;
                case "anchorMaxX": rect.anchorMax = new Vector2(N(), rect.anchorMax.y); break; case "anchorMaxY": rect.anchorMax = new Vector2(rect.anchorMax.x, N()); break;
                case "pivotX": rect.pivot = new Vector2(N(), rect.pivot.y); break; case "pivotY": rect.pivot = new Vector2(rect.pivot.x, N()); break;
                case "scaleX": rect.localScale = new Vector3(N(), rect.localScale.y, rect.localScale.z); break; case "scaleY": rect.localScale = new Vector3(rect.localScale.x, N(), rect.localScale.z); break;
                case "rotation": rect.localRotation = Quaternion.Euler(0, 0, N()); break;
            }
        }
        foreach (var component in after.Elements().Where(x => x.Name == "Image" || x.Name == "Text"))
        {
            var prior = before.Element(component.Name);
            foreach (var value in component.Attributes().Where(a => (string)prior.Attribute(a.Name) != a.Value))
                NativeVisualProperty.Apply(rect, component.Name.LocalName, value.Name.LocalName, value.Value,
                    key => SettingsAuthoringBridge.ResolveImage(workspace.Documents["images.xml"].Root.Elements("Image").Single(x => (string)x.Attribute("key") == key), publication));
        }
    }
    private static XElement[] Map(GameObject root, ResolvedUiDocument resolved)
    {
        string Id(string path) => AuthoringWorkspace.Hash(Encoding.UTF8.GetBytes(Source + "|" + path)).Substring(0, 32);
        var dictionary = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(x => Id(AnimationUtility.CalculateTransformPath(x, root.transform)), x => x);
        var rows = root.GetComponentsInChildren<UI_SoundSettingSlot>(true);
        foreach (var row in rows)
        {
            string prefix = Id("instance:" + AnimationUtility.CalculateTransformPath(row.transform, root.transform)) + "/";
            foreach (var first in rows[0].GetComponentsInChildren<RectTransform>(true))
            {
                string relative = AnimationUtility.CalculateTransformPath(first, rows[0].transform);
                dictionary[prefix + Id(AnimationUtility.CalculateTransformPath(first, root.transform))] = (RectTransform)At(row.transform, relative);
            }
        }
        return resolved.Root.Descendants("Element").Select(node =>
        {
            string id = (string)node.Attribute("id"); var rect = dictionary[id];
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rect, out string guid, out long fileId);
            if (fileId == 0) throw new InvalidOperationException("Source object has no persistent file ID");
            return new XElement("Map", new XAttribute("id", id), new XAttribute("path", AnimationUtility.CalculateTransformPath(rect, root.transform)), new XAttribute("fileId", fileId));
        }).ToArray();
    }
    private static XElement[] Remap(GameObject root, XElement previous)
    {
        if ((string)previous.Attribute("guid") != AssetDatabase.AssetPathToGUID(Source)) throw new FormatException("Original prefab GUID changed");
        var native = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(x => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out string guid, out long id); return id.ToString(); });
        return previous.Element("Mappings").Elements("Map").Select(x =>
        {
            var copy = new XElement(x); if (!native.TryGetValue((string)x.Attribute("fileId"), out var rect)) throw new FormatException("Original element was removed. Resolve this source change before re-exporting.");
            copy.SetAttributeValue("path", AnimationUtility.CalculateTransformPath(rect, root.transform)); return copy;
        }).ToArray();
    }
    private static Transform At(Transform root, string path) => string.IsNullOrEmpty(path) ? root : root.Find(path) ?? throw new FormatException("Native element missing: " + path);
    private static AnimationCurve Curve(Ease ease)
    {
        var keys = Enumerable.Range(0, 1025).Select(i => new Keyframe(i / 1024f, DOVirtual.EasedValue(0, 1, i / 1024f, ease))).ToArray();
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) { AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear); }
        return curve;
    }
    private static void Write(string directory, string path, byte[] bytes) { string target = AuthoringWorkspace.SafePath(directory, path); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.WriteAllBytes(target, bytes); }
}
