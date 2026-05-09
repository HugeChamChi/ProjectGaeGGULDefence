using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools > Create LevelUp Data Assets 실행 시
/// Assets/WorkSpace/USW/Data/LevelupSelection/ 에 LevelUpData 에셋 일괄 생성.
/// 시트 출처: https://docs.google.com/spreadsheets/d/1f8RdzwKxB25ydOxOatfc9AdYX0vlOYmT-LYEC4Rm5cc
///
/// 생성 제외 항목:
///   - 다중강화 (LevelUpData 구조상 effectType 1개만 지원)
///   - 특수기능 (별도 로직 구현 필요)
///   - 위엄(Sovereignty) — ChieftainAttackPercent 미구현
/// </summary>
public static class LevelUpDataCreator
{
    private const string OutputPath = "Assets/WorkSpace/USW/Data/LevelupSelection";

    [MenuItem("Tools/Create LevelUp Data Assets")]
    public static void CreateAll()
    {
        EnsureFolder();

        int created = 0;

        // ── 노말 — 스탯 강화 ──────────────────────────────────────────
        created += Create("BasicStrength",    LevelUpEffectType.AttackPercent,          10f, "기초 근력\n공격력 10% 증가");
        created += Create("WindTouch",        LevelUpEffectType.AttackSpeedPercent,     10f, "바람의 손길\n공격 속도 10% 증가");
        created += Create("ResonatingTotem",  LevelUpEffectType.TotemEfficiencyPercent, 20f, "공명하는 토템\n토템 효율 20% 증가");
        created += Create("SharpEye",         LevelUpEffectType.CritChancePercent,      10f, "관찰력\n치명타 확률 10% 증가");
        created += Create("DeadlyStrike",     LevelUpEffectType.CritDamagePercent,      10f, "치명적 타격\n치명타 데미지 10% 증가");
        created += Create("FoodStockpile",    LevelUpEffectType.FoodSpeedPercent,       10f, "식량 비축\n식량 생산 속도 10% 증가");
        created += Create("Hypertrophy",      LevelUpEffectType.ProjectileSizePercent,  10f, "비대\n투사체 크기 10% 증가");
        created += Create("VitalCharge",      LevelUpEffectType.GaugeSpeedPercent,      10f, "활력 충전\n게이지 회복 속도 10% 증가");
        // 9번 위엄(Sovereignty) — ChieftainAttackPercent 미구현으로 제외
        created += Create("FrontFirepower",   LevelUpEffectType.FrontRowAttackPercent,  15f, "전방 화력\n전방 2줄 기물 공격력 15% 증가");
        created += Create("RearSupport",      LevelUpEffectType.BackRowAttackPercent,   15f, "후방 지원\n후방 2줄 기물 공격력 15% 증가");
        created += Create("FrontAssault",     LevelUpEffectType.FrontRowSpeedPercent,   15f, "전방 침투\n전방 2줄 기물 공격 속도 15% 증가");
        created += Create("RearAcceleration", LevelUpEffectType.BackRowSpeedPercent,    15f, "후방 가속\n후방 2줄 기물 공격 속도 15% 증가");

        // ── 레어 — 스탯 강화 ──────────────────────────────────────────
        created += Create("PowerEnhancement", LevelUpEffectType.AttackPercent,          20f, "근력 강화\n공격력 20% 증가");
        created += Create("Gale",             LevelUpEffectType.AttackSpeedPercent,     20f, "질풍\n공격 속도 20% 증가");
        created += Create("TotemMastery",     LevelUpEffectType.TotemEfficiencyPercent, 30f, "토템 마스터리\n토템 효율 30% 증가");
        created += Create("Insight",          LevelUpEffectType.CritChancePercent,      20f, "통찰력\n치명타 확률 20% 증가");
        created += Create("FatalBlow",        LevelUpEffectType.CritDamagePercent,      20f, "치명적 일격\n치명타 데미지 20% 증가");
        created += Create("Gigantism",        LevelUpEffectType.ProjectileSizePercent,  30f, "거대화\n투사체 크기 30% 증가");
        created += Create("CycleLoop",        LevelUpEffectType.GaugeSpeedPercent,      20f, "순환의 고리\n게이지 회복 속도 20% 증가");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string msg = $"{OutputPath}\n에 {created}개 LevelUpData 에셋이 생성되었습니다.\n\n제외된 항목:\n• 위엄 — 족장 공격력 (미구현)\n• 다중강화 14종 — LevelUpData 구조 확장 필요\n• 특수기능 26종 — 별도 로직 필요";
        Debug.Log($"[LevelUpDataCreator] 완료 — {created}개 생성");
        EditorUtility.DisplayDialog("생성 완료", msg, "확인");
    }

    private static int Create(string key, LevelUpEffectType effectType, float value, string description)
    {
        string path = $"{OutputPath}/LevelUpData_{key}.asset";

        if (File.Exists(Path.GetFullPath(path)))
        {
            Debug.LogWarning($"[LevelUpDataCreator] 이미 존재 — 스킵: LevelUpData_{key}.asset");
            return 0;
        }

        var asset = ScriptableObject.CreateInstance<LevelUpData>();
        asset.effectType  = effectType;
        asset.value       = value;
        asset.description = description;
        asset.frameRate   = 12f;

        AssetDatabase.CreateAsset(asset, path);
        return 1;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/WorkSpace/USW/Data"))
            AssetDatabase.CreateFolder("Assets/WorkSpace/USW", "Data");

        if (!AssetDatabase.IsValidFolder(OutputPath))
            AssetDatabase.CreateFolder("Assets/WorkSpace/USW/Data", "LevelupSelection");
    }
}
