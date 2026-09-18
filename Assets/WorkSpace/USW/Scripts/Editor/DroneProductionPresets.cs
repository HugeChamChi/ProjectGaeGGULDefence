using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>DroneSelection19/20을 생성하고 기존 드론 풀에 명시적으로 연결한다.</summary>
public static class DroneProductionPresets
{
    /// <summary>기존 카드 설정은 보존하며 없는 카드만 생성한다.</summary>
    [MenuItem("Tools/Selections/Install Drone Selection 19 and 20")]
    public static void Install()
    {
        const string folder = "Assets/WorkSpace/USW/Data/SelectionData/";
        var pool = AssetDatabase.LoadAssetAtPath<LevelUpPoolData>(folder + "DroneLevelUpPool.asset");
        if (pool == null) throw new InvalidOperationException("DroneLevelUpPool is missing.");
        var cards = new List<LevelUpData>(pool.Cards);
        for (int number = 19; number <= 20; number++)
        {
            string path = folder + "DroneSelection" + number + ".asset";
            var card = AssetDatabase.LoadAssetAtPath<LevelUpData>(path);
            if (card == null)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:LevelUpData"))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<LevelUpData>(AssetDatabase.GUIDToAssetPath(guid));
                    if (existing.chooseId == 9100 + number) throw new InvalidOperationException("Duplicate card ID: " + existing.chooseId);
                }
                card = ScriptableObject.CreateInstance<LevelUpData>();
                card.chooseId = 9100 + number;
                DroneSelectionPresets.Configure(card, number == 19 ? DroneSelectionKind.MergeSupport : DroneSelectionKind.ExtraCombatDrone);
                AssetDatabase.CreateAsset(card, path);
            }
            if (!cards.Contains(card)) cards.Add(card);
        }
        Undo.RecordObject(pool, "Connect drone selections 19/20");
        pool.Cards = cards.ToArray();
        EditorUtility.SetDirty(pool);
        AssetDatabase.SaveAssetIfDirty(pool);
    }
}
