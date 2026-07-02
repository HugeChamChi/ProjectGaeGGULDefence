using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Collections.Generic;

public class AddressableDataSetup : EditorWindow
{
    [MenuItem("Tools/Addressables/Auto Setup Data Addresses")]
    public static void SetupAddresses()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable Asset Settings not found! Please create it from Window > Asset Management > Addressables > Groups.");
            return;
        }

        AddressableAssetGroup defaultGroup = settings.DefaultGroup;

        // Process UnitData
        string[] unitGuids = AssetDatabase.FindAssets("t:UnitData");
        foreach (string guid in unitGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnitData unitData = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            if (unitData != null)
            {
                bool changed = false;

                if (unitData.icon != null)
                {
                    string iconPath = AssetDatabase.GetAssetPath(unitData.icon);
                    string iconGuid = AssetDatabase.AssetPathToGUID(iconPath);
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(iconGuid, defaultGroup);
                    if (entry != null)
                    {
                        entry.SetAddress(unitData.icon.name);
                        unitData.iconAddress = entry.address;
                        changed = true;
                    }
                }

                if (unitData.prefab != null)
                {
                    string prefabPath = AssetDatabase.GetAssetPath(unitData.prefab);
                    string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(prefabGuid, defaultGroup);
                    if (entry != null)
                    {
                        entry.SetAddress(unitData.prefab.name);
                        unitData.prefabAddress = entry.address;
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(unitData);
                }
            }
        }

        // Process TotemData
        string[] totemGuids = AssetDatabase.FindAssets("t:TotemData");
        foreach (string guid in totemGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TotemData totemData = AssetDatabase.LoadAssetAtPath<TotemData>(path);
            if (totemData != null)
            {
                bool changed = false;

                if (totemData.icon != null)
                {
                    string iconPath = AssetDatabase.GetAssetPath(totemData.icon);
                    string iconGuid = AssetDatabase.AssetPathToGUID(iconPath);
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(iconGuid, defaultGroup);
                    if (entry != null)
                    {
                        entry.SetAddress(totemData.icon.name);
                        totemData.iconAddress = entry.address;
                        changed = true;
                    }
                }

                if (totemData.prefab != null)
                {
                    string prefabPath = AssetDatabase.GetAssetPath(totemData.prefab);
                    string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(prefabGuid, defaultGroup);
                    if (entry != null)
                    {
                        entry.SetAddress(totemData.prefab.name);
                        totemData.prefabAddress = entry.address;
                        changed = true;
                    }
                }

                // If TotemData has rotationSpriteAddresses like the subagent mentioned
                if (totemData.rotationSprites != null && totemData.rotationSprites.Length > 0)
                {
                    if (totemData.rotationSpriteAddresses == null || totemData.rotationSpriteAddresses.Length != totemData.rotationSprites.Length)
                    {
                        totemData.rotationSpriteAddresses = new string[totemData.rotationSprites.Length];
                    }

                    for (int i = 0; i < totemData.rotationSprites.Length; i++)
                    {
                        if (totemData.rotationSprites[i] != null)
                        {
                            string sPath = AssetDatabase.GetAssetPath(totemData.rotationSprites[i]);
                            string sGuid = AssetDatabase.AssetPathToGUID(sPath);
                            AddressableAssetEntry entry = settings.CreateOrMoveEntry(sGuid, defaultGroup);
                            if (entry != null)
                            {
                                entry.SetAddress(totemData.rotationSprites[i].name);
                                totemData.rotationSpriteAddresses[i] = entry.address;
                                changed = true;
                            }
                        }
                    }
                }

                if (changed)
                {
                    EditorUtility.SetDirty(totemData);
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Addressables setup for UnitData and TotemData complete!");
    }
}
