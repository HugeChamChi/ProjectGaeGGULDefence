using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>
/// TD1011~1014 신규 토템 4종(2026-10-11 사용자 선택)을 TD1009 SO/프리팹 복제로 만든다.
/// 기획: design/quick-specs/totem-idea-pool-2026-10-11.md §9.
/// 다시 실행하면 같은 경로의 SO/프리팹 구성을 덮어쓴다. 그림은 기존 토템 그림을 임시로 쓴다.
/// </summary>
public static class TotemBatchTD1011Builder
{
    private const string Folder = "Assets/WorkSpace/USW/Data/TotemData/Playable";
    private const string TemplateId = "TD1009";
    private const string RewardCanvasPath = "Assets/WorkSpace/USW/Prefab/UI/TotemRewardCanvas.prefab";

    private sealed class Spec
    {
        public int Id;
        public string ArtSourceId;
        public string Description;
        public bool UsesTotemCount;
        public System.Action<TotemData> Configure;
    }

    [MenuItem("Tools/USW/Totems/Create TD1011-1014")]
    public static void Build()
    {
        var specs = new[]
        {
            new Spec
            {
                Id = 1011, ArtSourceId = "TD1009",
                Description = "위 3칸은 공격할 수 없고, 아래 1칸의 공격 속도가 40% 상승합니다.",
                Configure = data =>
                {
                    data.attackDisabledRanges.Add(Offsets(new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1)));
                    data.effectRanges.Add(Offsets(new Vector2Int(0, -1)));
                    data.functions.Add(new SimpleBuffFunction { kind = StatKind.Speed, amount = 0.4f });
                }
            },
            new Spec
            {
                Id = 1012, ArtSourceId = "TD1006", UsesTotemCount = true,
                Description = "필드의 토템 1개당 치명타 확률이 2%, 치명타 피해가 3% 상승합니다.",
                Configure = data =>
                {
                    // TotemTotemCount가 수치만 읽어 전역으로 적용한다. 범위 없음.
                    data.functions.Add(new SimpleBuffFunction { kind = StatKind.CritChance, amount = 0.02f });
                    data.functions.Add(new SimpleBuffFunction { kind = StatKind.CritDamage, amount = 0.03f });
                }
            },
            new Spec
            {
                Id = 1013, ArtSourceId = "TD1008",
                Description = "같은 가로줄의 모든 유닛 공격력이 15% 상승합니다.",
                Configure = data =>
                {
                    data.effectRanges.Add(new DirectionalLineRange { direction = TotemDirection.Left, rotationAware = false });
                    data.effectRanges.Add(new DirectionalLineRange { direction = TotemDirection.Right, rotationAware = false });
                    data.functions.Add(new SimpleBuffFunction { kind = StatKind.AttackPercent, amount = 0.15f });
                }
            },
            new Spec
            {
                Id = 1014, ArtSourceId = "TD1007",
                Description = "보스가 등장하면 7초 동안 오른쪽 3칸의 공격 속도가 40% 상승합니다.",
                Configure = data =>
                {
                    data.effectRanges.Add(Offsets(new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(1, -1)));
                    data.functions.Add(new ConditionalBuffFunction
                    {
                        condition = new BossEncounterWindowCondition { seconds = 7f },
                        buff = new SimpleBuffFunction { kind = StatKind.Speed, amount = 0.4f }
                    });
                }
            },
        };

        var created = new List<TotemData>();
        foreach (var spec in specs) created.Add(BuildOne(spec));
        AddToRewardPool(created);
        AssetDatabase.SaveAssets();

        foreach (var data in created) TotemDataValidator.Validate(data);
        Debug.Log($"[TotemBatchTD1011Builder] Built and validated {string.Join(", ", created.Select(d => d.name))}.");
    }

    private static TotemData BuildOne(Spec spec)
    {
        string id = $"TD{spec.Id}";
        string dataPath = $"{Folder}/{id}Data.asset";
        string prefabPath = $"{Folder}/{id}.prefab";

        if (AssetDatabase.LoadAssetAtPath<TotemData>(dataPath) == null)
            AssetDatabase.CopyAsset($"{Folder}/{TemplateId}Data.asset", dataPath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            AssetDatabase.CopyAsset($"{Folder}/{TemplateId}.prefab", prefabPath);

        var data = AssetDatabase.LoadAssetAtPath<TotemData>(dataPath);
        var art = AssetDatabase.LoadAssetAtPath<TotemData>($"{Folder}/{spec.ArtSourceId}Data.asset");

        data.totemId = spec.Id;
        data.totemName = $"{id}(미정)";
        data.tier = Tier.Normal;
        data.description = spec.Description;
        data.UseSheetData = false;
        data.isRotatable = false;
        data.prefabAddress = id;
        data.iconAddress = art.iconAddress;
        data.rotationSpriteAddresses = (string[])art.rotationSpriteAddresses.Clone();
        data.icon = art.icon;
        data.rotationSprites = (Sprite[])art.rotationSprites.Clone();
        data.EffectGroups = new List<TotemEffectGroup>();
        data.GrowthStages = new List<TotemGrowthStage>();
        data.TierUpgrades = new List<TotemTierUpgrade>();
        data.functions = new List<ITotemFunction>();
        data.effectRanges = new List<ITotemRange>();
        data.attackDisabledRanges = new List<ITotemRange>();
        spec.Configure(data);

        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        data.prefab = prefabAsset;
        EditorUtility.SetDirty(data);

        ConfigurePrefab(prefabPath, data, spec.UsesTotemCount);
        RegisterAddress(prefabPath, id);
        return data;
    }

    private static void ConfigurePrefab(string prefabPath, TotemData data, bool usesTotemCount)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            root.name = data.name.Replace("Data", "");
            TotemBase behavior = root.GetComponent<TotemBase>();
            if (usesTotemCount && behavior is not TotemTotemCount)
            {
                Object.DestroyImmediate(behavior, true);
                behavior = root.AddComponent<TotemTotemCount>();
            }
            var serialized = new SerializedObject(behavior);
            serialized.FindProperty("totemData").objectReferenceValue = data;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RegisterAddress(string assetPath, string address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), settings.DefaultGroup);
        entry.address = address;
        EditorUtility.SetDirty(settings);
    }

    private static void AddToRewardPool(List<TotemData> totems)
    {
        var root = PrefabUtility.LoadPrefabContents(RewardCanvasPath);
        try
        {
            var reward = root.GetComponentInChildren<TotemRewardUI>(true);
            var serialized = new SerializedObject(reward);
            var pool = serialized.FindProperty("_totemPool");
            foreach (var totem in totems)
            {
                bool exists = false;
                for (int i = 0; i < pool.arraySize; i++)
                    if (pool.GetArrayElementAtIndex(i).objectReferenceValue == totem) exists = true;
                if (exists) continue;
                pool.InsertArrayElementAtIndex(pool.arraySize);
                pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = totem;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, RewardCanvasPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static TotemRelativeOffsetRange Offsets(params Vector2Int[] offsets)
        => new TotemRelativeOffsetRange { offsets = offsets.ToList() };
}
