using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class AddressableSetup
{
    [MenuItem("Custom/Register AudioManager")]
    public static void RegisterAudioManager()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("AddressableAssetSettings not found!");
            return;
        }

        AddressableAssetGroup group = settings.DefaultGroup;
        
        // Find the prefab
        string[] guids = AssetDatabase.FindAssets("AudioManager t:Prefab");
        if (guids.Length == 0)
        {
            Debug.LogError("AudioManager prefab not found!");
            return;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        Debug.Log("Found AudioManager at: " + assetPath);

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guids[0], group);
        entry.address = "AudioManager";

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        
        Debug.Log("Successfully registered AudioManager to Addressables!");
    }
}
