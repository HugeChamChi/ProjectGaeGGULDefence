using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Tools > USW > Create Drone Unit Assets
/// 드론 유닛 5종 × 4등급 = 20개 SO 에셋을 일괄 생성한다.
/// 이미 존재하는 파일은 덮어쓰지 않는다.
/// </summary>
public static class DroneUnitAssetCreator
{
    private const string OutputPath = "Assets/WorkSpace/USW/Data/DroneUnits";

    [MenuItem("Tools/USW/Create Drone Unit Assets")]
    public static void CreateAll()
    {
        EnsureDirectory(OutputPath);

        CreateDroneProducerAssets();
        CreateDroneBufferAssets();
        CreateDebuffDroneAssets();
        CreateDroneFoodProducerAssets();
        CreateDroneChieftainAssets();
        CreateDroneUnitDataAssets();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DroneUnitAssetCreator] 드론 유닛 SO 에셋 40개 생성 완료 → " + OutputPath);
    }

    // ── 드론 생산자 ─────────────────────────────────────────────────

    private static void CreateDroneProducerAssets()
    {
        var n = GetOrCreate<DroneProducerData>($"{OutputPath}/DroneProducerData_Normal.asset");
        n.maxDroneCount = 1; n.selfDestructCount = 0;
        n.droneAtk = 8f;  n.droneAttackInterval = 1.5f; n.selfDestructDamage = 0f;

        var r = GetOrCreate<DroneProducerData>($"{OutputPath}/DroneProducerData_Rare.asset");
        r.maxDroneCount = 2; r.selfDestructCount = 0;
        r.droneAtk = 12f; r.droneAttackInterval = 1.4f; r.selfDestructDamage = 0f;

        var e = GetOrCreate<DroneProducerData>($"{OutputPath}/DroneProducerData_Epic.asset");
        e.maxDroneCount = 3; e.selfDestructCount = 0;
        e.droneAtk = 17f; e.droneAttackInterval = 1.3f; e.selfDestructDamage = 0f;

        var l = GetOrCreate<DroneProducerData>($"{OutputPath}/DroneProducerData_Legend.asset");
        l.maxDroneCount = 4; l.selfDestructCount = 2;
        l.droneAtk = 20f; l.droneAttackInterval = 1.2f; l.selfDestructDamage = 80f;
    }

    // ── 드론 버퍼 ───────────────────────────────────────────────────

    private static void CreateDroneBufferAssets()
    {
        var n = GetOrCreate<DroneBufferData>($"{OutputPath}/DroneBufferData_Normal.asset");
        n.atkBuffMultiplier = 1.15f; n.speedBuffMultiplier = 1.12f; n.buffDuration = 5f;

        var r = GetOrCreate<DroneBufferData>($"{OutputPath}/DroneBufferData_Rare.asset");
        r.atkBuffMultiplier = 1.25f; r.speedBuffMultiplier = 1.20f; r.buffDuration = 7f;

        var e = GetOrCreate<DroneBufferData>($"{OutputPath}/DroneBufferData_Epic.asset");
        e.atkBuffMultiplier = 1.38f; e.speedBuffMultiplier = 1.30f; e.buffDuration = 10f;

        var l = GetOrCreate<DroneBufferData>($"{OutputPath}/DroneBufferData_Legend.asset");
        l.atkBuffMultiplier = 1.55f; l.speedBuffMultiplier = 1.45f; l.buffDuration = 12f;
    }

    // ── 디버프 드론 ─────────────────────────────────────────────────

    private static void CreateDebuffDroneAssets()
    {
        var n = GetOrCreate<DebuffDroneData>($"{OutputPath}/DebuffDroneData_Normal.asset");
        n.damageAmplificationMultiplier = 1.15f; n.debuffDuration = 6f;

        var r = GetOrCreate<DebuffDroneData>($"{OutputPath}/DebuffDroneData_Rare.asset");
        r.damageAmplificationMultiplier = 1.25f; r.debuffDuration = 9f;

        var e = GetOrCreate<DebuffDroneData>($"{OutputPath}/DebuffDroneData_Epic.asset");
        e.damageAmplificationMultiplier = 1.38f; e.debuffDuration = 12f;

        var l = GetOrCreate<DebuffDroneData>($"{OutputPath}/DebuffDroneData_Legend.asset");
        l.damageAmplificationMultiplier = 1.52f; l.debuffDuration = 14f;
    }

    // ── 드론 식량 생산자 ────────────────────────────────────────────

    private static void CreateDroneFoodProducerAssets()
    {
        var n = GetOrCreate<DroneFoodProducerData>($"{OutputPath}/DroneFoodProducerData_Normal.asset");
        n.fixedFoodPerSec = 1.5f; n.foodPerDronePerSec = 0.40f;

        var r = GetOrCreate<DroneFoodProducerData>($"{OutputPath}/DroneFoodProducerData_Rare.asset");
        r.fixedFoodPerSec = 2.5f; r.foodPerDronePerSec = 0.55f;

        var e = GetOrCreate<DroneFoodProducerData>($"{OutputPath}/DroneFoodProducerData_Epic.asset");
        e.fixedFoodPerSec = 4.0f; e.foodPerDronePerSec = 0.75f;

        var l = GetOrCreate<DroneFoodProducerData>($"{OutputPath}/DroneFoodProducerData_Legend.asset");
        l.fixedFoodPerSec = 6.0f; l.foodPerDronePerSec = 1.00f;
    }

    // ── 족장 ────────────────────────────────────────────────────────

    private static void CreateDroneChieftainAssets()
    {
        var n = GetOrCreate<DroneChieftainData>($"{OutputPath}/DroneChieftainData_Normal.asset");
        n.damagePerDrone = 25f;

        var r = GetOrCreate<DroneChieftainData>($"{OutputPath}/DroneChieftainData_Rare.asset");
        r.damagePerDrone = 42f;

        var e = GetOrCreate<DroneChieftainData>($"{OutputPath}/DroneChieftainData_Epic.asset");
        e.damagePerDrone = 68f;

        var l = GetOrCreate<DroneChieftainData>($"{OutputPath}/DroneChieftainData_Legend.asset");
        l.damagePerDrone = 110f;
    }

    // ── UnitData (UnitBase / UnitFactory 공통) ──────────────────────

    private static void CreateDroneUnitDataAssets()
    {
        // DroneProducer (unitType=5, characterId=5001~5004, atk=0, cooldown=12s 고정)
        CreateUnitData("DroneProducer_Normal", 5001, 5, Tier.Normal, 0f, 12f, "드론 생산자 (일반)");
        CreateUnitData("DroneProducer_Rare",   5002, 5, Tier.Rare,   0f, 12f, "드론 생산자 (레어)");
        CreateUnitData("DroneProducer_Epic",   5003, 5, Tier.Epic,   0f, 12f, "드론 생산자 (에픽)");
        CreateUnitData("DroneProducer_Legend", 5004, 5, Tier.Legend, 0f, 12f, "드론 생산자 (전설)");

        // DroneBuffer (unitType=6, characterId=5005~5008, cooldown 20→12s)
        CreateUnitData("DroneBuffer_Normal", 5005, 6, Tier.Normal, 0f, 20f, "드론 버퍼 (일반)");
        CreateUnitData("DroneBuffer_Rare",   5006, 6, Tier.Rare,   0f, 17f, "드론 버퍼 (레어)");
        CreateUnitData("DroneBuffer_Epic",   5007, 6, Tier.Epic,   0f, 14f, "드론 버퍼 (에픽)");
        CreateUnitData("DroneBuffer_Legend", 5008, 6, Tier.Legend, 0f, 12f, "드론 버퍼 (전설)");

        // DebuffDroneUnit (unitType=7, characterId=5009~5012, cooldown 25→16s)
        CreateUnitData("DebuffDroneUnit_Normal", 5009, 7, Tier.Normal, 0f, 25f, "디버프 드론 (일반)");
        CreateUnitData("DebuffDroneUnit_Rare",   5010, 7, Tier.Rare,   0f, 22f, "디버프 드론 (레어)");
        CreateUnitData("DebuffDroneUnit_Epic",   5011, 7, Tier.Epic,   0f, 19f, "디버프 드론 (에픽)");
        CreateUnitData("DebuffDroneUnit_Legend", 5012, 7, Tier.Legend, 0f, 16f, "디버프 드론 (전설)");

        // DroneFoodProducer (unitType=8, characterId=5013~5016, 패시브 — 스킬 없음)
        CreateUnitData("DroneFoodProducer_Normal", 5013, 8, Tier.Normal, 0f, 999f, "드론 식량 생산자 (일반)");
        CreateUnitData("DroneFoodProducer_Rare",   5014, 8, Tier.Rare,   0f, 999f, "드론 식량 생산자 (레어)");
        CreateUnitData("DroneFoodProducer_Epic",   5015, 8, Tier.Epic,   0f, 999f, "드론 식량 생산자 (에픽)");
        CreateUnitData("DroneFoodProducer_Legend", 5016, 8, Tier.Legend, 0f, 999f, "드론 식량 생산자 (전설)");

        // DroneChieftain (unitType=9, characterId=5017~5020, cooldown 25→15s)
        CreateUnitData("DroneChieftain_Normal", 5017, 9, Tier.Normal, 0f, 25f, "드론 족장 (일반)");
        CreateUnitData("DroneChieftain_Rare",   5018, 9, Tier.Rare,   0f, 22f, "드론 족장 (레어)");
        CreateUnitData("DroneChieftain_Epic",   5019, 9, Tier.Epic,   0f, 19f, "드론 족장 (에픽)");
        CreateUnitData("DroneChieftain_Legend", 5020, 9, Tier.Legend, 0f, 15f, "드론 족장 (전설)");
    }

    private static void CreateUnitData(
        string fileName, int characterId, int unitType,
        Tier tier, float atk, float skillCooldown, string unitName)
    {
        string path = $"{OutputPath}/UnitData_{fileName}.asset";
        var data = GetOrCreate<UnitData>(path);

        data.characterId  = characterId;
        data.unitType     = unitType;
        data.unitTier     = tier;
        data.unitTribe    = UnitTribe.UnEmployed; // 임시 — 드론 tribe 추가 시 교체
        data.unitName     = unitName;
        data.atk          = atk;
        data.skillCooldown = skillCooldown;
        // prefab, icon 은 Unity Inspector에서 직접 연결
        EditorUtility.SetDirty(data);
    }

    // ── 유틸 ────────────────────────────────────────────────────────

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureDirectory(string path)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", path);
        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);
    }
}
