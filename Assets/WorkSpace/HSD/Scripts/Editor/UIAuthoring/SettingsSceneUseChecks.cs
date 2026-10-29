using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GGD.UI.Authoring;
using HSD.UI.Setting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reads actual scene overrides using Unity's prefab model without saving or changing scene assets.</summary>
public static class SettingsSceneUseChecks
{
    /// <summary>Records which shared settings instances/overrides are present in the real lobby and ingame scenes.</summary>
    public static void Run()
    {
        var lines = new List<string>();
        foreach (string path in new[] { "Assets/Scenes/LobbyScene.unity", "Assets/Scenes/IngameScene.unity" })
        {
            string before = AuthoringWorkspace.Hash(File.ReadAllBytes(path));
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var settings = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<UI_SettingPanel_Base>(true)).ToArray();
                lines.Add(path + " : scene settings instances=" + settings.Length + " (lobby settings can be created dynamically from its registered popup prefab)");
                foreach (var host in settings)
                {
                    var originals = new HashSet<UnityEngine.Object>();
                    foreach (var component in host.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null) continue;
                        originals.Add(PrefabUtility.GetCorrespondingObjectFromSource(component));
                        originals.Add(PrefabUtility.GetCorrespondingObjectFromOriginalSource(component));
                        originals.Add(PrefabUtility.GetCorrespondingObjectFromSource(component.gameObject));
                        originals.Add(PrefabUtility.GetCorrespondingObjectFromOriginalSource(component.gameObject));
                    }
                    var prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(host.gameObject);
                    var changes = prefab == null ? Array.Empty<PropertyModification>() : PrefabUtility.GetPropertyModifications(prefab) ?? Array.Empty<PropertyModification>();
                    var relevant = changes.Where(x => x.target != null && originals.Contains(x.target)).ToArray();
                    lines.Add("HOST " + AnimationUtility.CalculateTransformPath(host.transform, null) + " | overrides=" + relevant.Length);
                    foreach (var change in relevant) lines.Add("  " + change.target.name + " " + change.target.GetType().Name + "." + change.propertyPath + "=" + change.value + (change.objectReference != null ? " -> " + AssetDatabase.GetAssetPath(change.objectReference) : ""));
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            if (before != AuthoringWorkspace.Hash(File.ReadAllBytes(path))) throw new InvalidOperationException("Read-only scene inspection modified " + path);
            lines.Add("UNCHANGED " + path);
        }
        File.WriteAllLines("outputs/ui-authoring-verification/scene-uses.txt", lines);
    }
}
