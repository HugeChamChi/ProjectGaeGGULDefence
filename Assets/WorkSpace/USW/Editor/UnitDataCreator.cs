using UnityEngine;
using UnityEditor;

/// <summary>
/// 유니티 메뉴: Tools > USW > Create UnitData Assets
/// IngameUnitData 폴더에 16개 UnitData 에셋 자동 생성
/// 이미 존재하는 에셋은 덮어쓰지 않고 건너뜀
/// </summary>
public static class UnitDataCreator
{
    private const string SavePath = "Assets/WorkSpace/USW/Data/IngameUnitData";

    private struct UnitEntry
    {
        public int    CharacterId;
        public string AssetName;
        public string UnitName;
        public Tier  Tier;
        public UnitTribe Tribe;
        public int   Atk;
        public int   SkillAtk;
        public float SkillCooldown;
        public float FoodPerTick;
    }

    private static readonly UnitEntry[] Entries =
    {
        // ── Frog (UnEmployed) ──────────────────────────────
        new UnitEntry { CharacterId=1001, AssetName="Frog_Normal",  UnitName="Frog", Tier=Tier.Normal, Tribe=UnitTribe.UnEmployed, Atk=120, SkillAtk=350,  SkillCooldown=12f,   FoodPerTick=10f },
        new UnitEntry { CharacterId=1002, AssetName="Frog_Rare",    UnitName="Frog", Tier=Tier.Rare,   Tribe=UnitTribe.UnEmployed, Atk=180, SkillAtk=525,  SkillCooldown=11.4f, FoodPerTick=10f },
        new UnitEntry { CharacterId=1003, AssetName="Frog_Epic",    UnitName="Frog", Tier=Tier.Epic,   Tribe=UnitTribe.UnEmployed, Atk=240, SkillAtk=700,  SkillCooldown=10.8f, FoodPerTick=10f },
        new UnitEntry { CharacterId=1004, AssetName="Frog_Legend",  UnitName="Frog", Tier=Tier.Legend, Tribe=UnitTribe.UnEmployed, Atk=360, SkillAtk=1050, SkillCooldown=9.6f,  FoodPerTick=10f },

        // ── Frog_Gunner ────────────────────────────────────
        new UnitEntry { CharacterId=1005, AssetName="Gunner_Normal", UnitName="Frog_Gunner", Tier=Tier.Normal, Tribe=UnitTribe.Gunner, Atk=150, SkillAtk=400,  SkillCooldown=10f,  FoodPerTick=10f },
        new UnitEntry { CharacterId=1006, AssetName="Gunner_Rare",   UnitName="Frog_Gunner", Tier=Tier.Rare,   Tribe=UnitTribe.Gunner, Atk=225, SkillAtk=600,  SkillCooldown=9.5f, FoodPerTick=10f },
        new UnitEntry { CharacterId=1007, AssetName="Gunner_Epic",   UnitName="Frog_Gunner", Tier=Tier.Epic,   Tribe=UnitTribe.Gunner, Atk=300, SkillAtk=800,  SkillCooldown=9f,   FoodPerTick=10f },
        new UnitEntry { CharacterId=1008, AssetName="Gunner_Legend", UnitName="Frog_Gunner", Tier=Tier.Legend, Tribe=UnitTribe.Gunner, Atk=450, SkillAtk=1200, SkillCooldown=8f,   FoodPerTick=10f },

        // ── Frog_Ninja ─────────────────────────────────────
        new UnitEntry { CharacterId=1009, AssetName="Ninja_Normal", UnitName="Frog_Ninja", Tier=Tier.Normal, Tribe=UnitTribe.Ninja, Atk=140, SkillAtk=420,  SkillCooldown=9f,   FoodPerTick=10f },
        new UnitEntry { CharacterId=1010, AssetName="Ninja_Rare",   UnitName="Frog_Ninja", Tier=Tier.Rare,   Tribe=UnitTribe.Ninja, Atk=210, SkillAtk=630,  SkillCooldown=8.5f, FoodPerTick=10f },
        new UnitEntry { CharacterId=1011, AssetName="Ninja_Epic",   UnitName="Frog_Ninja", Tier=Tier.Epic,   Tribe=UnitTribe.Ninja, Atk=280, SkillAtk=840,  SkillCooldown=8.1f, FoodPerTick=10f },
        new UnitEntry { CharacterId=1012, AssetName="Ninja_Legend", UnitName="Frog_Ninja", Tier=Tier.Legend, Tribe=UnitTribe.Ninja, Atk=420, SkillAtk=1260, SkillCooldown=7.2f, FoodPerTick=10f },

        // ── Frog_Wizard ────────────────────────────────────
        new UnitEntry { CharacterId=1013, AssetName="Wizard_Normal", UnitName="Frog_Wizard", Tier=Tier.Normal, Tribe=UnitTribe.Wizard, Atk=90,  SkillAtk=550,  SkillCooldown=14f,   FoodPerTick=10f },
        new UnitEntry { CharacterId=1014, AssetName="Wizard_Rare",   UnitName="Frog_Wizard", Tier=Tier.Rare,   Tribe=UnitTribe.Wizard, Atk=135, SkillAtk=825,  SkillCooldown=13.3f, FoodPerTick=10f },
        new UnitEntry { CharacterId=1015, AssetName="Wizard_Epic",   UnitName="Frog_Wizard", Tier=Tier.Epic,   Tribe=UnitTribe.Wizard, Atk=180, SkillAtk=1100, SkillCooldown=12.6f, FoodPerTick=10f },
        new UnitEntry { CharacterId=1016, AssetName="Wizard_Legend", UnitName="Frog_Wizard", Tier=Tier.Legend, Tribe=UnitTribe.Wizard, Atk=270, SkillAtk=1650, SkillCooldown=11.2f, FoodPerTick=10f },
    };

    [MenuItem("Tools/USW/Create UnitData Assets")]
    public static void CreateAll()
    {
        int created = 0;
        int skipped = 0;

        foreach (var e in Entries)
        {
            string fullPath = $"{SavePath}/{e.AssetName}.asset";

            if (AssetDatabase.LoadAssetAtPath<UnitData>(fullPath) != null)
            {
                Debug.Log($"[UnitDataCreator] 건너뜀 (이미 존재): {e.AssetName}");
                skipped++;
                continue;
            }

            var data = ScriptableObject.CreateInstance<UnitData>();
            data.characterId   = e.CharacterId;
            data.unitType      = e.CharacterId; // characterId와 동일하게 초기 설정
            data.unitName      = e.UnitName;
            data.unitTier      = e.Tier;
            data.unitTribe     = e.Tribe;
            data.atk           = e.Atk;
            data.skillAtk      = e.SkillAtk;
            data.skillCooldown = e.SkillCooldown;
            data.foodPerTick   = e.FoodPerTick;

            AssetDatabase.CreateAsset(data, fullPath);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[UnitDataCreator] 완료 — 생성: {created}개, 건너뜀: {skipped}개");
        EditorUtility.DisplayDialog(
            "UnitData 생성 완료",
            $"생성: {created}개\n건너뜀(이미 존재): {skipped}개\n\n저장 위치: {SavePath}",
            "확인");
    }
}
