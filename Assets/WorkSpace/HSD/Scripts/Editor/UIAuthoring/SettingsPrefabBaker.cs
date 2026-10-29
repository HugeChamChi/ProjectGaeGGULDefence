using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GGD.UI.Authoring;
using GGD.UI.Authoring.Studio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bakes the validated publication into a hidden prefab with persistent shared materials and serialized binding references.</summary>
public static class SettingsPrefabBaker
{
    /// <summary>Content-addressed candidates keep an unsuccessful bake from overwriting the installed prefab.</summary>
    public const string OutputRoot = "Assets/WorkSpace/HSD/Generated/UiAuthoring/Baked";

    /// <summary>Constructs the graph in a private preview scene and saves it without touching gameplay scenes or host prefabs.</summary>
    public static BakedUiDocument Bake(AuthoringWorkspace workspace, AuthoringAssets.FontEntry[] fonts, AuthoringAssets.ImageEntry[] images, string contentHash)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Bake settings in Edit mode");
        string directory = OutputRoot + "/" + contentHash.Substring(0, 16) + "-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(directory); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var resources = ScriptableObject.CreateInstance<AuthoringAssets>(); resources.Fonts = fonts; resources.Images = images;
        var scene = EditorSceneManager.NewPreviewScene(); AuthoredUiView view = null;
        try
        {
            var parent = new GameObject("Bake host", typeof(RectTransform), typeof(Canvas)); SceneManager.MoveGameObjectToScene(parent, scene);
            view = AuthoredUiView.Create(UiComposer.Resolve(workspace, "Screens/UI3030.xml"), (RectTransform)parent.transform,
                new AuthoringResources(resources), new StudioBindings(workspace.Catalog.Root));
            view.Root.name = "Authored UI3030";
            var baked = view.Root.AddComponent<BakedUiDocument>(); baked.Capture(view, contentHash);
            var materials = new Dictionary<Material, Material>();
            foreach (var text in view.Root.GetComponentsInChildren<TMP_Text>(true))
            {
                var original = text.fontSharedMaterial;
                if (AssetDatabase.Contains(original)) continue;
                if (!materials.TryGetValue(original, out var persistent))
                {
                    string path = directory + "/Outline" + materials.Count + ".mat";
                    persistent = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (persistent == null) { persistent = new Material(original) { name = "UI3030 outline " + materials.Count }; AssetDatabase.CreateAsset(persistent, path); }
                    else { EditorUtility.CopySerialized(original, persistent); EditorUtility.SetDirty(persistent); }
                    materials.Add(original, persistent);
                }
                text.fontSharedMaterial = persistent;
            }
            view.Root.SetActive(false); AssetDatabase.SaveAssets();
            string prefabPath = directory + "/UI3030.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(view.Root, prefabPath);
            if (prefab == null) throw new IOException("Settings prefab bake failed");
            var result = prefab.GetComponent<BakedUiDocument>();
            if (result == null || result.ElementCount != view.Elements.Count || result.ContentHash != contentHash || result.gameObject.activeSelf)
                throw new FormatException("Settings prefab failed its bake validation");
            return result;
        }
        finally
        {
            view?.Dispose(); EditorSceneManager.ClosePreviewScene(scene);
            // This is the temporary, unsaved resource table created above, never an installed asset.
            if (resources != null && !EditorUtility.IsPersistent(resources)) UnityEngine.Object.DestroyImmediate(resources);
        }
    }

    /// <summary>Rejects missing, stale or active-root prefabs before entering a player build.</summary>
    public static void Validate(AuthoringAssets assets)
    {
        if (assets.BakedScreen == null || assets.BakedScreen.ContentHash != assets.ContentHash || assets.BakedScreen.gameObject.activeSelf)
            throw new UnityEditor.Build.BuildFailedException("Authored settings prefab is missing or stale. Install Published Settings to bake it again.");
    }
}
