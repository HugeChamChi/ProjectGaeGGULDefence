#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 용사 파티 5종(Warrior/Mage/Archer/Rogue/Priest)을 Test_Party에서 바로 플레이 테스트할 수 있도록
/// 임시 프리팹(Frog.prefab 템플릿 복제 — 아트 준비 전까지 플레이스홀더 비주얼) + UnitData를
/// 자동 생성하고 Addressable 등록, Test_Party 등록까지 일괄 처리하는 임시 개발용 툴.
/// 실제 아트/수치가 준비되면 이 스크립트로 만든 프리팹/UnitData는 교체되어야 합니다.
/// </summary>
public static class HeroPartyTestSetup
{
    private const string TemplatePrefabPath = "Assets/WorkSpace/HSD/Prefab/Unit/Frog.prefab";
    private const string PrefabOutputDir = "Assets/WorkSpace/USW/Prefab/InGameUnit/HeroParty";
    private const string DataOutputDir = "Assets/WorkSpace/USW/Data/HeroUnits";
    private const string TestPartyPath = "Assets/WorkSpace/HSD/Data/Party/Test_Party.asset";

    private class HeroSpec
    {
        public string Name;
        public System.Type ScriptType;
        public UnitTribe Tribe;
        public int CharacterId;
        public float Atk;
        public float AttackSpeed;
        public float SkillCooldown;
    }

    [MenuItem("Tools/GaeGGul/Setup Hero Party Test Data")]
    public static void Setup()
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);
        if (template == null)
        {
            Debug.LogError($"[HeroPartyTestSetup] 템플릿 프리팹을 찾을 수 없습니다: {TemplatePrefabPath}");
            return;
        }

        CreateFolderRecursive(PrefabOutputDir);
        CreateFolderRecursive(DataOutputDir);

        var specs = new List<HeroSpec>
        {
            new HeroSpec { Name = "Frog_Warrior", ScriptType = typeof(FrogWarrior), Tribe = UnitTribe.Warrior, CharacterId = 1016, Atk = 100, AttackSpeed = 1f, SkillCooldown = 10 },
            new HeroSpec { Name = "Frog_Mage",    ScriptType = typeof(FrogMage),    Tribe = UnitTribe.Mage,    CharacterId = 1017, Atk = 80,  AttackSpeed = 1f, SkillCooldown = 8 },
            new HeroSpec { Name = "Frog_Archer",  ScriptType = typeof(FrogArcher),  Tribe = UnitTribe.Archer,  CharacterId = 1018, Atk = 90,  AttackSpeed = 1f, SkillCooldown = 8 },
            new HeroSpec { Name = "Frog_Rogue",   ScriptType = typeof(FrogRogue),   Tribe = UnitTribe.Rogue,   CharacterId = 1019, Atk = 90,  AttackSpeed = 1f, SkillCooldown = 8 },
            new HeroSpec { Name = "Frog_Priest",  ScriptType = typeof(FrogPriest),  Tribe = UnitTribe.Support, CharacterId = 1020, Atk = 70,  AttackSpeed = 1f, SkillCooldown = 12 },
        };

        var createdUnitData = new List<UnitData>();

        foreach (var spec in specs)
        {
            string prefabPath = $"{PrefabOutputDir}/{spec.Name}.prefab";

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
            var oldUnit = instance.GetComponent<UnitBase>();
            if (oldUnit != null) Object.DestroyImmediate(oldUnit);
            instance.AddComponent(spec.ScriptType);
            instance.name = spec.Name;

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var unitData = ScriptableObject.CreateInstance<UnitData>();
            unitData.characterId = spec.CharacterId;
            unitData.unitType = spec.CharacterId;
            unitData.unitName = spec.Name;
            unitData.unitTier = Tier.Normal;
            unitData.unitTribe = spec.Tribe;
            unitData.atk = spec.Atk;
            unitData.attackSpeed = spec.AttackSpeed;
            unitData.skillCooldown = spec.SkillCooldown;
            unitData.populationCost = 1;
            unitData.prefab = prefabAsset;

            string dataPath = $"{DataOutputDir}/UnitData_{spec.Name}_Normal.asset";
            AssetDatabase.CreateAsset(unitData, dataPath);
            createdUnitData.Add(unitData);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 기존 Addressables 자동 등록 툴 재사용 (icon/prefab 참조 -> Address 자동 세팅)
        AddressableDataSetup.SetupAddresses();

        var party = AssetDatabase.LoadAssetAtPath<PartyDataSO>(TestPartyPath);
        if (party != null)
        {
            if (party.unitDataList == null) party.unitDataList = new List<UnitData>();
            foreach (var ud in createdUnitData)
            {
                if (!party.unitDataList.Contains(ud))
                    party.unitDataList.Add(ud);
            }
            EditorUtility.SetDirty(party);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HeroPartyTestSetup] Test_Party에 {createdUnitData.Count}종 등록 완료.");
        }
        else
        {
            Debug.LogWarning($"[HeroPartyTestSetup] Test_Party를 찾을 수 없습니다: {TestPartyPath}");
        }

        Debug.Log("[HeroPartyTestSetup] 용사 파티 테스트 데이터 세팅 완료!");
    }

    private static void CreateFolderRecursive(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
