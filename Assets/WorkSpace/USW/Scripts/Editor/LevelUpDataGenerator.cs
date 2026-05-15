#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools > USW > Generate LevelUp Assets 실행 시
/// Assets/WorkSpace/USW/Data/LevelupSelection/ 에 60개 LevelUpData SO 자동 생성
/// (시트 choose_id 3000~3059 전체 대응)
/// </summary>
public static class LevelUpDataGenerator
{
    private const string OutputDir = "Assets/WorkSpace/USW/Data/LevelupSelection";

    private struct ItemDef
    {
        public int                  id;
        public string               name;
        public Tier                 grade;
        public float                rate;
        public string               desc;
        public UnitTribe[]          tribes;
        public LevelUpEffectType    pe;     // primary effect
        public float                pv;     // primary value
        public LevelUpEffectType    se;     // secondary effect
        public float                sv;     // secondary value
        public LevelUpSpecialEffect sp;     // special effect
        public float                spv;   // special value
    }

    private static readonly UnitTribe[] AllTribes  = System.Array.Empty<UnitTribe>();
    private static readonly UnitTribe[] Ninja       = { UnitTribe.Ninja };
    private static readonly UnitTribe[] Gunner      = { UnitTribe.Gunner };
    private static readonly UnitTribe[] Wizard      = { UnitTribe.Wizard };
    private static readonly UnitTribe[] Unemployed  = { UnitTribe.UnEmployed };

    // 타입 별명
    private const LevelUpEffectType    ETN  = LevelUpEffectType.None;
    private const LevelUpEffectType    ETA  = LevelUpEffectType.AttackPercent;
    private const LevelUpEffectType    ETAS = LevelUpEffectType.AttackSpeedPercent;
    private const LevelUpEffectType    ETTE = LevelUpEffectType.TotemEfficiencyPercent;
    private const LevelUpEffectType    ETCC = LevelUpEffectType.CritChancePercent;
    private const LevelUpEffectType    ETCD = LevelUpEffectType.CritDamagePercent;
    private const LevelUpEffectType    ETFP = LevelUpEffectType.FoodProductionPercent;
    private const LevelUpEffectType    ETPS = LevelUpEffectType.ProjectileSizePercent;
    private const LevelUpEffectType    ETGS = LevelUpEffectType.GaugeSpeedPercent;
    private const LevelUpEffectType    ETEX = LevelUpEffectType.ExpGainPercent;
    private const LevelUpEffectType    ETFA = LevelUpEffectType.FrontRowAttackPercent;
    private const LevelUpEffectType    ETBA = LevelUpEffectType.BackRowAttackPercent;
    private const LevelUpEffectType    ETFS = LevelUpEffectType.FrontRowSpeedPercent;
    private const LevelUpEffectType    ETBS = LevelUpEffectType.BackRowSpeedPercent;
    private const LevelUpEffectType    ETCA = LevelUpEffectType.ChieftainAttackPercent;
    private const LevelUpSpecialEffect SPN  = LevelUpSpecialEffect.None;

    private static readonly ItemDef[] Items =
    {
        // ── Normal (3000~3024) ──────────────────────────────────
        new ItemDef { id=3000, name="기초 근력",    grade=Tier.Normal, rate=0.020f,
            desc="공격력 10% 증가",                         pe=ETA,  pv=10 },
        new ItemDef { id=3001, name="바람의 손길",  grade=Tier.Normal, rate=0.020f,
            desc="공격 속도 10% 증가",                      pe=ETAS, pv=10 },
        new ItemDef { id=3002, name="공명하는 토템",grade=Tier.Normal, rate=0.020f,
            desc="토템 효율 20% 증가",                      pe=ETTE, pv=20 },
        new ItemDef { id=3003, name="관찰력",       grade=Tier.Normal, rate=0.020f,
            desc="치명타 확률 10% 증가",                    pe=ETCC, pv=10 },
        new ItemDef { id=3004, name="치명적 타격",  grade=Tier.Normal, rate=0.020f,
            desc="치명타 데미지 10% 증가",                  pe=ETCD, pv=10 },
        new ItemDef { id=3005, name="식량 비축",    grade=Tier.Normal, rate=0.020f,
            desc="식량 생산 속도 10% 증가",                 pe=ETFP, pv=10 },
        new ItemDef { id=3006, name="비대",         grade=Tier.Normal, rate=0.020f,
            desc="투사체 크기 10% 증가",                    pe=ETPS, pv=10 },
        new ItemDef { id=3007, name="활력 충전",    grade=Tier.Normal, rate=0.020f,
            desc="쿨타임 10% 감소",                         pe=ETGS, pv=10 },
        new ItemDef { id=3008, name="위엄",         grade=Tier.Normal, rate=0.020f,
            desc="족장의 공격력 10% 증가",                  pe=ETCA, pv=10 },
        new ItemDef { id=3009, name="전방 화력",    grade=Tier.Normal, rate=0.020f,
            desc="전방 2줄 기물 공격력 15% 증가",           pe=ETFA, pv=15 },
        new ItemDef { id=3010, name="후방 지원",    grade=Tier.Normal, rate=0.020f,
            desc="후방 2줄 기물 공격력 15% 증가",           pe=ETBA, pv=15 },
        new ItemDef { id=3011, name="전방 침투",    grade=Tier.Normal, rate=0.020f,
            desc="전방 2줄 기물 공격 속도 15% 증가",        pe=ETFS, pv=15 },
        new ItemDef { id=3012, name="후방 가속",    grade=Tier.Normal, rate=0.020f,
            desc="후방 2줄 기물 공격 속도 15% 증가",        pe=ETBS, pv=15 },
        new ItemDef { id=3013, name="경량 연타",    grade=Tier.Normal, rate=0.020f,
            desc="공격력 -10%, 공격속도 20% 증가",          pe=ETA, pv=-10, se=ETAS, sv=20 },
        new ItemDef { id=3014, name="묵직한 한 방", grade=Tier.Normal, rate=0.020f,
            desc="공격속도 -40%, 공격력 40% 증가",          pe=ETA, pv=40,  se=ETAS, sv=-40 },
        new ItemDef { id=3015, name="보급관",       grade=Tier.Normal, rate=0.020f,
            desc="식량 200개 즉시 획득",
            sp=LevelUpSpecialEffect.GiveFoodAmount, spv=200 },
        new ItemDef { id=3016, name="연속 공격",    grade=Tier.Normal, rate=0.020f,
            desc="10회 공격 시 1회 추가 공격",
            sp=LevelUpSpecialEffect.AttackEveryNHits, spv=10 },
        new ItemDef { id=3017, name="변칙 타격",    grade=Tier.Normal, rate=0.020f,
            desc="공격 시 5% 확률로 50%의 공격력으로 추가 데미지",
            pe=ETN, pv=50, sp=LevelUpSpecialEffect.RandomProcAttack, spv=5 },
        new ItemDef { id=3018, name="폭발적인 기운",grade=Tier.Normal, rate=0.020f,
            desc="게이지가 가득차면 1초간 공격력 20% 증가",
            pe=ETN, pv=20, sp=LevelUpSpecialEffect.BurstOnSkillFull, spv=1 },
        new ItemDef { id=3019, name="할인 티켓",    grade=Tier.Normal, rate=0.020f,
            desc="소환 비용 5% 할인",
            sp=LevelUpSpecialEffect.SummonDiscount, spv=5 },
        new ItemDef { id=3020, name="정밀 연타",    grade=Tier.Normal, rate=0.020f,
            desc="10회 공격 시 1회 추가 공격",
            sp=LevelUpSpecialEffect.AttackEveryNHits, spv=10 },
        new ItemDef { id=3021, name="영감의 원천",  grade=Tier.Normal, rate=0.020f,
            desc="경험치 획득량 10% 증가",                  pe=ETEX, pv=10 },
        new ItemDef { id=3022, name="지원군",       grade=Tier.Normal, rate=0.020f,
            desc="[Normal~Rare] 무작위 기물 획득",
            sp=LevelUpSpecialEffect.GainRandomUnit },
        new ItemDef { id=3023, name="영토 확장",    grade=Tier.Normal, rate=0.020f,
            desc="최대 인구수 1 증가",
            sp=LevelUpSpecialEffect.PopulationIncrease, spv=1 },
        new ItemDef { id=3024, name="서비스",       grade=Tier.Normal, rate=0.020f,
            desc="기물 판매 시 식량 5개 추가 획득",
            sp=LevelUpSpecialEffect.SellBonusFood, spv=5 },

        // ── Rare (3025~3044) ────────────────────────────────────
        new ItemDef { id=3025, name="노련한 총잡이",grade=Tier.Rare, rate=0.015f,
            desc="[Rare~Epic] 총잡이 기물 획득",
            sp=LevelUpSpecialEffect.GainGunnerUnit },
        new ItemDef { id=3026, name="중급 닌자",    grade=Tier.Rare, rate=0.015f,
            desc="[Rare~Epic] 닌자 기물 획득",
            sp=LevelUpSpecialEffect.GainNinjaUnit },
        new ItemDef { id=3027, name="서클 메이지",  grade=Tier.Rare, rate=0.015f,
            desc="[Rare~Epic] 마법사 기물 획득",
            sp=LevelUpSpecialEffect.GainWizardUnit },
        new ItemDef { id=3028, name="건실한 노동자",grade=Tier.Rare, rate=0.015f,
            desc="[Rare~Epic] 무직 기물 획득",
            sp=LevelUpSpecialEffect.GainUnemployedUnit },
        new ItemDef { id=3029, name="대형 군량미",  grade=Tier.Rare, rate=0.015f,
            desc="식량 500개 즉시 획득",
            sp=LevelUpSpecialEffect.GiveFoodAmount, spv=500 },
        new ItemDef { id=3030, name="토템 소환",    grade=Tier.Rare, rate=0.015f,
            desc="토템 선택지 즉시 출력",
            sp=LevelUpSpecialEffect.TriggerTotemSelection },
        new ItemDef { id=3031, name="공간 왜곡",    grade=Tier.Rare, rate=0.015f,
            desc="토템을 겹쳐 배치할 수 있도록 변경",
            sp=LevelUpSpecialEffect.AllowTotemOverlap },
        new ItemDef { id=3032, name="근력 강화",    grade=Tier.Rare, rate=0.015f,
            desc="공격력 20% 증가",                         pe=ETA,  pv=20 },
        new ItemDef { id=3033, name="질풍",         grade=Tier.Rare, rate=0.015f,
            desc="공격 속도 20% 증가",                      pe=ETAS, pv=20 },
        new ItemDef { id=3034, name="토템 마스터리",grade=Tier.Rare, rate=0.015f,
            desc="토템 효율 30% 증가",                      pe=ETTE, pv=30 },
        new ItemDef { id=3035, name="통찰력",       grade=Tier.Rare, rate=0.015f,
            desc="치명타 확률 20% 증가",                    pe=ETCC, pv=20 },
        new ItemDef { id=3036, name="치명적 일격",  grade=Tier.Rare, rate=0.015f,
            desc="치명타 데미지 20% 증가",                  pe=ETCD, pv=20 },
        new ItemDef { id=3037, name="거대화",       grade=Tier.Rare, rate=0.015f,
            desc="투사체 크기 30% 증가",                    pe=ETPS, pv=30 },
        new ItemDef { id=3038, name="순환의 고리",  grade=Tier.Rare, rate=0.015f,
            desc="쿨타임 20% 감소",                         pe=ETGS, pv=20 },
        new ItemDef { id=3039, name="질량탄",       grade=Tier.Rare, rate=0.015f,
            desc="투사체 크기 20% 증가, 공격속도 10% 증가", pe=ETPS, pv=20, se=ETAS, sv=10 },
        new ItemDef { id=3040, name="중량탄",       grade=Tier.Rare, rate=0.015f,
            desc="투사체 크기 10% 증가, 공격력 10% 증가",   pe=ETA,  pv=10, se=ETPS, sv=10 },
        new ItemDef { id=3041, name="진로 계승",    grade=Tier.Rare, rate=0.015f,
            desc="기물 합성 시 재료와 같은 직업이 추가 등장",
            sp=LevelUpSpecialEffect.MergeKeepsTribe },
        new ItemDef { id=3042, name="만개",         grade=Tier.Rare, rate=0.015f,
            desc="식량 게이지가 가득차면 1회 추가 공격",
            sp=LevelUpSpecialEffect.ExtraAttackOnSkillFull },
        new ItemDef { id=3043, name="폭풍 연격",    grade=Tier.Rare, rate=0.015f,
            desc="5회 공격 시 1회 추가 공격",
            sp=LevelUpSpecialEffect.AttackEveryNHits, spv=5 },
        new ItemDef { id=3044, name="자폭병",       grade=Tier.Rare, rate=0.015f,
            desc="기물 판매 시 기물 공격력 100%의 피해",
            sp=LevelUpSpecialEffect.SellDealsDamage, spv=100 },

        // ── Epic (3045~3054) ────────────────────────────────────
        new ItemDef { id=3045, name="풍요로운 영토",grade=Tier.Epic, rate=0.015f,
            desc="최대 인구수 2 증가",
            sp=LevelUpSpecialEffect.PopulationIncrease, spv=2 },
        new ItemDef { id=3046, name="낙뢰 마법",    grade=Tier.Epic, rate=0.015f,
            desc="마법사 기본공격 제거. 게이지 가득차면 번개 마법 사용",
            tribes=Wizard, sp=LevelUpSpecialEffect.WizardLightningMode },
        new ItemDef { id=3047, name="연쇄 타격",    grade=Tier.Epic, rate=0.015f,
            desc="공격 시 30% 확률로 추가 공격",
            sp=LevelUpSpecialEffect.RandomBonusAttack, spv=30 },
        new ItemDef { id=3048, name="닌자 비술서",  grade=Tier.Epic, rate=0.015f,
            desc="닌자 공격력 30%, 공격속도 20% 증가",
            tribes=Ninja, pe=ETN, pv=30, se=ETN, sv=20,
            sp=LevelUpSpecialEffect.BuffNinjaTribe },
        new ItemDef { id=3049, name="더블 탭",      grade=Tier.Epic, rate=0.015f,
            desc="총잡이 공격력 30%, 공격속도 20% 증가",
            tribes=Gunner, pe=ETN, pv=30, se=ETN, sv=20,
            sp=LevelUpSpecialEffect.BuffGunnerTribe },
        new ItemDef { id=3050, name="대마법의 흐름",grade=Tier.Epic, rate=0.015f,
            desc="마법사 공격력 30%, 쿨타임 20% 감소",
            tribes=Wizard, pe=ETN, pv=30, se=ETN, sv=20,
            sp=LevelUpSpecialEffect.BuffWizardTribe },
        new ItemDef { id=3051, name="질량 가속도",  grade=Tier.Epic, rate=0.015f,
            desc="투사체 크기 10%당 공격력 20% 증가",
            pe=ETN, pv=20, sp=LevelUpSpecialEffect.ProjectileSizeScalesAtk },
        new ItemDef { id=3052, name="고속 가동",    grade=Tier.Epic, rate=0.015f,
            desc="공격력 -50%, 공격속도 100% 증가",         pe=ETA, pv=-50, se=ETAS, sv=100 },
        new ItemDef { id=3053, name="양손잡이",     grade=Tier.Epic, rate=0.015f,
            desc="공격력 50% 감소, 공격 시 1회 추가 공격",
            pe=ETA, pv=-50, sp=LevelUpSpecialEffect.ExtraAttackEveryAttack },
        new ItemDef { id=3054, name="극한의 각성",  grade=Tier.Epic, rate=0.015f,
            desc="게이지가 가득차면 3초간 공격력 100% 증가",
            pe=ETN, pv=100, sp=LevelUpSpecialEffect.BurstOnSkillFull, spv=3 },

        // ── Legend (3055~3059) ───────────────────────────────────
        new ItemDef { id=3055, name="물리 마법사",  grade=Tier.Legend, rate=0.010f,
            desc="마법사 스킬 제거. 공격력, 공격속도 100% 증가",
            tribes=Wizard, pe=ETN, pv=100, sp=LevelUpSpecialEffect.WizardPhysicalMode },
        new ItemDef { id=3056, name="식충이",       grade=Tier.Legend, rate=0.010f,
            desc="무직 식량 획득 제거. 게이지 가득찰 때마다 공격력 1 증가",
            tribes=Unemployed, pe=ETN, pv=1, sp=LevelUpSpecialEffect.UnemployedFoodNegate },
        new ItemDef { id=3057, name="마력 치환",    grade=Tier.Legend, rate=0.010f,
            desc="식량 생산량 60% 감소, 쿨타임 200% 감소",  pe=ETFP, pv=-60, se=ETGS, sv=200 },
        new ItemDef { id=3058, name="원맨쇼",       grade=Tier.Legend, rate=0.010f,
            desc="기물 판매 시 족장 공격력 5% 상승. 인구수 +2 이상시 1당 공격력 10% 감소",
            pe=ETN, pv=5, se=ETN, sv=10, sp=LevelUpSpecialEffect.ChieftainGainOnSell },
        new ItemDef { id=3059, name="광폭화",       grade=Tier.Legend, rate=0.010f,
            desc="공격력 90% 감소, 공격속도 1000% 증가",    pe=ETA, pv=-90, se=ETAS, sv=1000 },
    };

    [MenuItem("Tools/USW/Generate LevelUp Assets")]
    public static void Generate()
    {
        if (!Directory.Exists(OutputDir))
            Directory.CreateDirectory(OutputDir);

        // 기존 에셋 삭제
        string[] existing = AssetDatabase.FindAssets("t:LevelUpData", new[] { OutputDir });
        foreach (var guid in existing)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.DeleteAsset(path);
        }

        int created = 0;
        foreach (var item in Items)
        {
            var asset = ScriptableObject.CreateInstance<LevelUpData>();
            asset.chooseId        = item.id;
            asset.chooseName      = item.name;
            asset.tier            = item.grade;
            asset.spawnRate       = item.rate;
            asset.description     = item.desc;
            asset.applicableTribes = item.tribes ?? AllTribes;
            asset.primaryEffect   = item.pe;
            asset.primaryValue    = item.pv;
            asset.secondaryEffect = item.se;
            asset.secondaryValue  = item.sv;
            asset.specialEffect   = item.sp;
            asset.specialValue    = item.spv;

            string safeName = item.name.Replace(" ", "_").Replace("/", "_");
            string assetPath = $"{OutputDir}/LevelUpData_{item.id}_{safeName}.asset";
            AssetDatabase.CreateAsset(asset, assetPath);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LevelUpDataGenerator] {created}개 LevelUpData 에셋 생성 완료 → {OutputDir}");
        EditorUtility.DisplayDialog("완료", $"{created}개 LevelUpData 에셋이 생성되었습니다.", "확인");
    }
}
#endif
