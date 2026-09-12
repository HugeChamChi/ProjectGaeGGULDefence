using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

/// <summary>사용자가 제작한 토템 SO의 동작 설정과 Addressables 프리팹 연결을 검증한다.</summary>
public static class TotemDataValidator
{
    /// <summary>선택한 TotemData만 검사한다. 에셋을 수정하지 않는다.</summary>
    [MenuItem("Assets/Totems/Validate Selected SO")]
    public static void ValidateSelected()
    {
        int count = 0;
        foreach (var selected in Selection.objects)
        {
            if (selected is not TotemData data) continue;
            Validate(data);
            count++;
        }
        Debug.Log($"[TotemDataValidator] Validated {count} selected TotemData assets.");
    }

    /// <summary>동작 스크립트가 요구하는 데이터 계약을 검사한다.</summary>
    public static void Validate(TotemData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var prefab = ResolvePrefab(data.prefab, data.prefabAddress);
        var behavior = prefab != null ? prefab.GetComponent<TotemBase>() : null;
        if (prefab != null && behavior == null) Fail(data, "프리팹에 TotemBase 계열 스크립트가 없습니다.");
        if (!string.IsNullOrEmpty(data.prefabAddress) && prefab == null) Fail(data, "Prefab Address를 찾을 수 없습니다.");
        if (data.UseSheetData && behavior is not null && behavior is not GenericBuffTotem)
            Debug.LogWarning($"[{data.name}] SO 기능 구성을 유지하려면 Use Sheet Data를 끄세요.", data);

        if (behavior is TotemBonusProjectile)
        {
            var settings = data.BonusProjectile;
            if (settings == null || !IsFinite(settings.Chance) || settings.Chance < 0 || settings.Chance > 1 ||
                !IsFinite(settings.AttackCoefficient) || settings.AttackCoefficient <= 0)
                Fail(data, "보조 투사체 확률(0~1)/공격력 계수를 확인하세요.");
        }
        if (behavior is TotemKillRangeGrowth) ValidateGrowth(data);
        if (behavior is TotemWildcardSpawner) ValidateWildcard(data);
        if (behavior is TotemTemporaryTierBoost)
        {
            ValidateOneCell(data);
            var keys = new HashSet<(UnitData, Tier)>();
            foreach (var entry in data.TierUpgrades)
            {
                if (entry == null || entry.OriginalData == null || entry.UpgradedData == null ||
                    entry.OriginalTier < Tier.Normal || entry.OriginalTier >= Tier.Legend)
                    Fail(data, "상위 데이터 매핑은 원본/상위 SO와 Normal~Epic 원본 등급이 필요합니다.");
                if (!keys.Add((entry.OriginalData, entry.OriginalTier))) Fail(data, "상위 데이터 매핑이 중복됩니다.");
            }
        }
        ValidateDebuff(data);
    }

    private static void ValidateGrowth(TotemData data)
    {
        bool hasInitial = false;
        var thresholds = new HashSet<int>();
        foreach (var stage in data.GrowthStages)
        {
            if (stage == null || stage.RequiredKills < 0 || stage.Offsets == null || stage.Offsets.Count == 0)
                Fail(data, "각 성장 단계에 처치 수와 범위를 설정하세요.");
            if (!thresholds.Add(stage.RequiredKills)) Fail(data, "성장 단계의 처치 수가 중복됩니다.");
            hasInitial |= stage.RequiredKills == 0;
        }
        if (!hasInitial) Fail(data, "처치 수 0의 초기 범위가 필요합니다.");
    }

    private static void ValidateWildcard(TotemData data)
    {
        var settings = data.WildcardSpawn;
        if (settings?.Unit == null || !IsFinite(settings.IntervalSeconds) || settings.IntervalSeconds <= 0)
            Fail(data, "조커 UnitData와 양수 생성 주기를 설정하세요.");
        var unit = settings.Unit;
        var prefab = ResolvePrefab(unit.prefab, unit.prefabAddress);
        if (prefab == null || prefab.GetComponent<WildcardUnit>() == null) Fail(data, "조커 프리팹에는 WildcardUnit이 필요합니다.");
        if (unit.atk.normal != 0 || unit.foodProduction.normal != 0)
            Fail(data, "조커의 노말 공격력/식량 생산량은 0이어야 합니다.");
    }

    private static void ValidateDebuff(TotemData data)
    {
        if (!data.TryGetDebuffBinding(out var binding)) return;
        _ = new DebuffBinding(binding.DebuffId, binding.Trigger, binding.StacksPerApply);
        var settings = Resources.Load<DebuffSettings>("DebuffSettings");
        if (settings == null) Fail(data, "Resources/DebuffSettings가 없습니다.");
        var catalog = new DebuffCatalog(settings);
        if (!catalog.TryGet(binding.DebuffId, out var definition)) Fail(data, "디버프 ID를 찾을 수 없습니다.");
        if (binding.Trigger == DebuffTrigger.ProjectileHit)
        {
            if (double.IsNaN(data.DebuffFireInterval) || double.IsInfinity(data.DebuffFireInterval) ||
                data.DebuffFireInterval <= 0 || data.DebuffImpactDamage < 0)
                Fail(data, "디버프 발사 주기/즉발 피해를 확인하세요.");
        }
        else if (binding.Trigger == DebuffTrigger.AffectedUnitBasicAttackAttempt)
        {
            if (definition.Kind != DebuffKind.ArmorBreak || binding.StacksPerApply != 1)
                Fail(data, "일반 공격 아머는 ArmorBreak/+1이어야 합니다.");
            ValidateOneCell(data);
        }
        else Fail(data, "토템에서 지원하지 않는 디버프 발동 시점입니다.");
    }

    private static void ValidateOneCell(TotemData data)
    {
        var offsets = new HashSet<Vector2Int>(data.GetEffectPreviewOffsets());
        if (offsets.Count != 1 || offsets.Contains(Vector2Int.zero))
            Fail(data, "이 토템은 자기 칸 이외의 범위 한 칸을 지정해야 합니다.");
    }

    private static GameObject ResolvePrefab(GameObject direct, string address)
    {
        if (direct != null) return direct;
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null || string.IsNullOrEmpty(address)) return null;
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            foreach (var entry in group.entries)
                if (entry.address == address) return AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
        }
        return null;
    }

    /// <summary>선택한 SO에 편집 가능한 3×3/4×4/5×5 범위 프리셋을 넣는다.</summary>
    [MenuItem("Assets/Totems/Set TD1002 Growth Ranges")]
    public static void SetGrowthRanges()
    {
        foreach (var selected in Selection.objects)
        {
            if (selected is not TotemData data) continue;
            Undo.RecordObject(data, "Set totem growth ranges");
            data.GrowthStages = new List<TotemGrowthStage>();
            for (int kills = 0; kills <= 2; kills++)
            {
                int size = kills + 3;
                int start = -(size - 1) / 2;
                var stage = new TotemGrowthStage { RequiredKills = kills };
                for (int y = start; y < start + size; y++)
                for (int x = start; x < start + size; x++) stage.Offsets.Add(new Vector2Int(x, y));
                data.GrowthStages.Add(stage);
            }
            EditorUtility.SetDirty(data);
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static void Fail(TotemData data, string message) => throw new InvalidOperationException($"[{data.name}] {message}");
}
