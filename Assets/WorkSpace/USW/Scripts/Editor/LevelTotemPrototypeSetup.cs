using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LevelTotemPrototypeSetup
{
    private const string DataRoot = "Assets/WorkSpace/USW/Data/TotemData";
    private const string Output = "Assets/WorkSpace/USW/Resources/LevelTotems";

    [MenuItem("Tools/Totem/Build Level Prototype")]
    public static void Build()
    {
        Folder(Output);
        var oldData = AssetDatabase.FindAssets("t:TotemData", new[] { DataRoot })
            .Select(g => AssetDatabase.LoadAssetAtPath<TotemData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        var sprites = oldData.Where(d => d != null).Select(d => d.iconAddress).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToArray();
        var patterns = new (string name, Vector2Int[] offsets)[]
        {
            ("전방", P(0,-1)), ("양옆", P(-1,0, 1,0)), ("대각", P(1,-1)),
            ("기사", P(1,-2)), ("직선", P(0,-1, 0,-2)), ("전열", P(-1,-1, 0,-1, 1,-1)),
            ("모서리", P(1,0, 0,-1)), ("삼각", P(1,0, 1,-1, 0,-1)),
            ("십자", P(-1,0, 1,0, 0,-1, 0,1)), ("쌍대각", P(-1,-1, 1,-1)),
            ("엑스", P(-1,-1, 1,-1, -1,1, 1,1)), ("도약", P(0,-2, 0,-3))
        };
        int id = 2001;
        foreach (var pattern in patterns)
        for (int amount = 2; amount <= 3; amount++)
        {
            string path = $"{Output}/LT{id}.asset";
            var data = AssetDatabase.LoadAssetAtPath<TotemData>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<TotemData>(); AssetDatabase.CreateAsset(data, path); }
            data.totemId = id++;
            data.totemName = $"{pattern.name} +{amount}";
            data.LevelIncrease = amount;
            data.tier = Tier.Normal;
            data.isRotatable = true;
            data.description = $"범위 안 유닛 레벨 [+{amount}]\n90도 회전 · 합산 · 최대 15";
            data.DetailedDescription = data.description + "\n범위를 벗어나면 레벨과 미달 해금 효과가 즉시 복구됩니다.";
            data.iconAddress = sprites.Length > 0 ? sprites[(id - 2002) % sprites.Length] : "";
            data.rotationSpriteAddresses = new[] { data.iconAddress, data.iconAddress, data.iconAddress, data.iconAddress };
            data.effectRanges = new List<ITotemRange> { new TotemRelativeOffsetRange { offsets = pattern.offsets.ToList() } };
            data.EffectGroups.Clear(); data.functions.Clear(); data.attackDisabledRanges.Clear();
            EditorUtility.SetDirty(data);
        }
        // AssetDatabase.MoveAsset preserves GUIDs, including Addressables references.
        Folder(DataRoot + "/Legacy");
        if (AssetDatabase.IsValidFolder(DataRoot + "/Playable")) Move(DataRoot + "/Playable", DataRoot + "/Legacy/Playable");
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { DataRoot }).Select(AssetDatabase.GUIDToAssetPath).ToArray())
            if (!path.Contains("/Legacy/") && !path.EndsWith("/GeneralTotemPrefab.prefab")) Move(path, DataRoot + "/Legacy/" + System.IO.Path.GetFileName(path));
        foreach (string path in AssetDatabase.FindAssets("t:TotemData", new[] { DataRoot }).Select(AssetDatabase.GUIDToAssetPath).ToArray())
            if (!path.Contains("/Legacy/")) Move(path, DataRoot + "/Legacy/" + System.IO.Path.GetFileName(path));
        const string scripts = "Assets/WorkSpace/USW/Scripts/Totem";
        Folder(scripts + "/Legacy");
        string[] dedicated = { "TotemAttackBuff", "TotemAttackTopBuff", "TotemBerserkBuff", "TotemBonusProjectile", "TotemBossKillStack", "TotemDisableBoost", "TotemFoodBuff", "TotemFoodGenerator", "TotemKillRangeGrowth", "TotemNullifyDebuff", "TotemOverWelmBuff", "TotemPositionBuff", "TotemProximityBuff", "TotemSellStack", "TotemShadowAttack", "TotemSpawnCharge", "TotemSpeedBuff", "TotemTemporaryTierBoost", "TotemTimedBuff", "TotemTotemCount", "TotemUnitedBuff", "TotemWildcardSpawner" };
        foreach (var name in dedicated)
            if (AssetDatabase.LoadAssetAtPath<MonoScript>($"{scripts}/{name}.cs") != null) Move($"{scripts}/{name}.cs", $"{scripts}/Legacy/{name}.cs");
        AssetDatabase.SaveAssets();
        Debug.Log("Level prototype built: 24 level totems; Legacy assets retain GUIDs.");
    }
    private static Vector2Int[] P(params int[] xy)
    { var result = new Vector2Int[xy.Length / 2]; for (int i = 0; i < result.Length; i++) result[i] = new Vector2Int(xy[i*2], xy[i*2+1]); return result; }
    private static void Folder(string path)
    { if (AssetDatabase.IsValidFolder(path)) return; string parent = System.IO.Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); }
    private static void Move(string from, string to)
    { string error = AssetDatabase.MoveAsset(from,to); if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error); }
}
