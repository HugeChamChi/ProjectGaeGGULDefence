using System;
using System.IO;
using System.Linq;
using System.Text;
using HSD.UI.Setting;
using UnityEditor;
using UnityEngine;

/// <summary>Reports Unity-resolved settings prefabs before migration. Does not modify scenes or prefabs.</summary>
public static class SettingsExportProbe
{
    /// <summary>Records the real hierarchy/components and private connection fields using Unity serialization.</summary>
    public static void Run()
    {
        Directory.CreateDirectory("outputs/ui-authoring-verification");
        var report = new StringBuilder();
        foreach (string path in new[] { "Assets/WorkSpace/HSD/Prefab/UI/InGame/InGame_Setting_Popup.prefab", "Assets/WorkSpace/USW/Prefabs/UI/LobbySettingsPopup.prefab", "Assets/WorkSpace/USW/Prefab/UI/IngameOptionsMenu.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                report.AppendLine("ASSET " + path);
                foreach (var transform in root.GetComponentsInChildren<RectTransform>(true))
                {
                    string relative = AnimationUtility.CalculateTransformPath(transform, root.transform);
                    report.AppendLine(relative + " | active=" + transform.gameObject.activeSelf + " | " + string.Join(",", transform.GetComponents<Component>().Where(x => x != null).Select(x => x.GetType().FullName)));
                }
                foreach (var panel in root.GetComponentsInChildren<UI_SettingPanel_Base>(true))
                {
                    var serialized = new SerializedObject(panel); var iterator = serialized.GetIterator();
                    while (iterator.NextVisible(true))
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue is Component component)
                            report.AppendLine("FIELD " + iterator.propertyPath + " -> " + AnimationUtility.CalculateTransformPath(component.transform, panel.transform));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        File.WriteAllText("outputs/ui-authoring-verification/extraction-probe.txt", report.ToString());
        Debug.Log("UI_AUTHORING_PROBE_DONE");
    }
}
