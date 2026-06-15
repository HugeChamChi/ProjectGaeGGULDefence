using UnityEngine;
using UnityEditor;

public class AssignLevelUpIcons : EditorWindow
{
    [MenuItem("Tools/Assign Level Up Icons")]
    public static void AssignIcons()
    {
        string dataPath = "Assets/WorkSpace/HSD/Data/LevelUpSelectData";
        string iconPath = "Assets/Imports/Selection";

        string[] guids = AssetDatabase.FindAssets("t:LevelUpData", new[] { dataPath });
        int updatedCount = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            LevelUpData data = AssetDatabase.LoadAssetAtPath<LevelUpData>(assetPath);

            if (data != null)
            {
                int id = data.chooseId;
                string spritePath = $"{iconPath}/Selection_Icon_{id}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                if (sprite != null)
                {
                    data.icon = sprite;
                    EditorUtility.SetDirty(data);
                    updatedCount++;
                }
                else
                {
                    Debug.LogWarning($"[AssignLevelUpIcons] Icon not found for ID {id} at {spritePath}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AssignLevelUpIcons] Successfully updated {updatedCount} LevelUpData assets.");
    }
}
