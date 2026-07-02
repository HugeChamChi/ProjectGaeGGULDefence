#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class AddressablesSetupAgent
{
    [MenuItem("Tools/Antigravity/Setup SO Addressables")]
    public static void SetupSOAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressablesSetupAgent] Addressable Asset Settings not found. Please create it first.");
            return;
        }

        // Data 그룹 찾기 또는 생성
        AddressableAssetGroup group = settings.FindGroup("Data");
        if (group == null)
        {
            group = settings.CreateGroup("Data", false, false, true, settings.DefaultGroup.Schemas);
        }

        // 1. Data 관련 SO 에셋 검색 (Assets/Data, USW 및 HSD 데이터 폴더 등)
        string[] searchPaths = new string[] { 
            "Assets/Data",
            "Assets/WorkSpace/USW/Data",
            "Assets/WorkSpace/HSD/Data",
            "Assets/WorkSpace/HSD/Data/DynamicShop"
        };
        
        List<string> validPaths = new List<string>();
        foreach (var path in searchPaths)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                validPaths.Add(path);
            }
        }

        if (validPaths.Count == 0)
        {
            Debug.LogWarning("[AddressablesSetupAgent] 검색할 Data 폴더가 존재하지 않습니다.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", validPaths.ToArray());
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            string fileName = Path.GetFileNameWithoutExtension(path);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            string groupName = "Data";
            string labelName = "Data";
            string address = $"Data/{fileName}";

            if (asset is DynamicShopData)
            {
                labelName = "DynamicShopData";
                address = $"DynamicShopData/{fileName}";
            }
            else if (asset is Test_CharacterData)
            {
                labelName = "CharacterData";
                address = $"CharacterData/{fileName}";
            }
            else if (asset is ChiefData)
            {
                labelName = "ChiefData";
                address = $"ChiefData/{fileName}";
            }
            else if (asset is ItemData)
            {
                labelName = "ItemData";
                address = $"ItemData/{fileName}";
            }
            else if (asset is IconItemDataSO)
            {
                labelName = "PlayerIcon";
                address = $"PlayerIcon/{fileName}";
            }
            else if (asset is FrameItemDataSO)
            {
                labelName = "PlayerFrame";
                address = $"PlayerFrame/{fileName}";
            }

            AddressableAssetGroup targetGroup = settings.FindGroup(groupName) ?? group;

            var entry = settings.CreateOrMoveEntry(guid, targetGroup, readOnly: false, postEvent: false);
            if (entry != null)
            {
                entry.SetAddress(address);
                entry.SetLabel(labelName, true, true);
                count++;
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[Antigravity] 성공적으로 {count}개의 ScriptableObject를 'Data' 그룹의 Addressables로 세팅했습니다!");
    }

    [MenuItem("Tools/Antigravity/Setup Scenes Addressables")]
    public static void SetupScenesAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AddressablesSetupAgent] Addressable Asset Settings not found.");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup("Scenes");
        if (group == null)
        {
            group = settings.CreateGroup("Scenes", false, false, true, settings.DefaultGroup.Schemas);
        }

        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/NotUse/")) continue;

            string fileName = Path.GetFileNameWithoutExtension(path);
            var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            if (entry != null)
            {
                entry.SetAddress(fileName);
                entry.SetLabel("Scenes", true, true);
                count++;
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Antigravity] 성공적으로 {count}개의 Scene을 'Scenes' 그룹의 Addressables로 세팅했습니다!");
    }
}
#endif
