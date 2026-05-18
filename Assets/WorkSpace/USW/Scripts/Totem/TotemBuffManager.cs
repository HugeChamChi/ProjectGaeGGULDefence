using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 토템 버프 수치 보관 + 전체 셀 버프 플래그 재계산
///
/// </summary>
public class TotemBuffManager : InGameSingleton<TotemBuffManager>
{
    // 공격력 배율 = 1 + 토템 누산 + 레벨업 누산
    private float _totemAttackBonus   = 0f;
    private float _levelUpAttackBonus = 0f;
    public float AttackMultiplier => 1f + _totemAttackBonus + _levelUpAttackBonus;

    // 속도 배율 (낮을수록 빠름) = Max(0.1, 1 - 토템 누산 - 레벨업 누산)
    private float _totemSpeedBonus   = 0f;
    private float _levelUpSpeedBonus = 0f;
    public float SpeedMultiplier => Mathf.Max(0.1f, 1f - _totemSpeedBonus - _levelUpSpeedBonus);
    // 식량 생산 속도 배율 (기본 1.0 — 낮을수록 식량 틱 빨라짐)
    public float FoodSpeedMultiplier { get; private set; } = 1f;
    // 식량 생산량 배율 (기본 1.0 — 고블린 마법사가 낮춤, 토템이 높임)
    public float FoodAmountMultiplier { get; private set; } = 1f;
    // 치명타 확률 보너스 (기본 0 — 토템으로 증가)
    public float CritChanceBonus      { get; private set; } = 0f;
    // 치명타 데미지 보너스 배율 (기본 0 — LevelUp 배율에 가산)
    public float CritDamageBonus      { get; private set; } = 0f;
    // 게이지 회복 속도 배율 (기본 1.0 — 낮을수록 게이지 빨라짐)
    public float GaugeSpeedMultiplier { get; private set; } = 1f;
    // 투사체 크기 배율 (기본 1.0 — 높을수록 커짐)
    public float ProjectileSizeMultiplier { get; private set; } = 1f;
    // 토템 효율 보너스 (기본 0 — 레벨업으로 증가, 토템 버프 적용 시 곱해짐)
    private float _totemEfficiencyBonus = 0f;

    // 활성 토템 목록 직접 관리 (FindObjectsOfType 대체)
    private readonly List<TotemBase> _activeTotem = new();

    // ── 토템 등록/해제 ─────────────────────────────────────────

    public int GetActiveTotemCount() => _activeTotem.Count;

    /// <summary>TotemBase.OnPlaced()에서 호출</summary>
    public void RegisterTotem(TotemBase totem)
    {
        if (!_activeTotem.Contains(totem))
            _activeTotem.Add(totem);
    }

    /// <summary>TotemBase.OnRemoved()에서 호출</summary>
    public void UnregisterTotem(TotemBase totem)
    {
        _activeTotem.Remove(totem);
    }

    // ── 공격력 버프 (토템 전용 — 토템효율 적용) ───────────────
    public void AddAttackBuff(float percent)
    {
        _totemAttackBonus += percent * (1f + _totemEfficiencyBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 공격력 +{percent * 100f:F0}% → 배율: {AttackMultiplier:F2}x");
#endif
    }

    public void RemoveAttackBuff(float percent)
    {
        _totemAttackBonus = Mathf.Max(0f, _totemAttackBonus - percent * (1f + _totemEfficiencyBonus));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 공격력 -{percent * 100f:F0}% 해제 → 배율: {AttackMultiplier:F2}x");
#endif
    }

    // ── 공격력 버프 (레벨업/판매 전용 — 토템효율 미적용) ──────
    public void AddLevelUpAttackBuff(float percent)
    {
        _levelUpAttackBonus += percent;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[LevelUp] 공격력 +{percent * 100f:F0}% → 배율: {AttackMultiplier:F2}x");
#endif
    }

    // ── 속도 버프 (토템 전용 — 토템효율 적용) ─────────────────
    public void AddSpeedBuff(float percent)
    {
        _totemSpeedBonus += percent * (1f + _totemEfficiencyBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 속도 +{percent * 100f:F0}% → 배율: {SpeedMultiplier:F2}x");
#endif
    }

    public void RemoveSpeedBuff(float percent)
    {
        _totemSpeedBonus = Mathf.Max(0f, _totemSpeedBonus - percent * (1f + _totemEfficiencyBonus));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 속도 -{percent * 100f:F0}% 해제 → 배율: {SpeedMultiplier:F2}x");
#endif
    }

    // ── 속도 버프 (레벨업 전용 — 토템효율 미적용) ─────────────
    public void AddLevelUpSpeedBuff(float percent)
    {
        _levelUpSpeedBonus += percent;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[LevelUp] 속도 +{percent * 100f:F0}% → 배율: {SpeedMultiplier:F2}x");
#endif
    }

    // ── 토템 효율 ──────────────────────────────────────────────
    public void AddTotemEfficiency(float bonus)
    {
        _totemEfficiencyBonus += bonus;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 토템 효율 +{bonus * 100f:F0}% → 보너스: {_totemEfficiencyBonus:F2}");
#endif
    }

    // ── 식량 속도 버프 ─────────────────────────────────────────
    public void AddFoodSpeedBuff(float percent)
    {
        FoodSpeedMultiplier = Mathf.Max(0.1f, FoodSpeedMultiplier - percent);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 식량속도 +{percent * 100f:F0}% → 배율: {FoodSpeedMultiplier:F2}x");
#endif
    }

    public void RemoveFoodSpeedBuff(float percent)
    {
        FoodSpeedMultiplier = Mathf.Min(1f, FoodSpeedMultiplier + percent);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 식량속도 -{percent * 100f:F0}% 해제 → 배율: {FoodSpeedMultiplier:F2}x");
#endif
    }

    // ── 치명타 버프 (토템) ─────────────────────────────────────
    public void AddCritChanceBuff(float percent)
    {
        CritChanceBonus += percent * (1f + _totemEfficiencyBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 치명타 확률 +{percent * 100f:F0}% → 보너스: {CritChanceBonus:F2}");
#endif
    }

    public void RemoveCritChanceBuff(float percent)
    {
        CritChanceBonus = Mathf.Max(0f, CritChanceBonus - percent * (1f + _totemEfficiencyBonus));
    }

    public void AddCritDamageBuff(float percent)
    {
        CritDamageBonus += percent * (1f + _totemEfficiencyBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 치명타 데미지 +{percent * 100f:F0}% → 보너스: {CritDamageBonus:F2}");
#endif
    }

    public void RemoveCritDamageBuff(float percent)
    {
        CritDamageBonus = Mathf.Max(0f, CritDamageBonus - percent * (1f + _totemEfficiencyBonus));
    }

    // ── 게이지 회복 속도 버프 ──────────────────────────────────
    public void AddGaugeSpeedBuff(float percent)
    {
        GaugeSpeedMultiplier = Mathf.Max(0.1f, GaugeSpeedMultiplier - percent);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 게이지속도 +{percent * 100f:F0}% → 배율: {GaugeSpeedMultiplier:F2}x");
#endif
    }

    // ── 투사체 크기 버프 ───────────────────────────────────────
    public void AddProjectileSizeBuff(float percent)
    {
        ProjectileSizeMultiplier += percent;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 투사체크기 +{percent * 100f:F0}% → 배율: {ProjectileSizeMultiplier:F2}x");
#endif
    }

    // ── 식량 생산량 버프 (토템) ────────────────────────────────
    public void AddFoodAmountBuff(float percent)
    {
        FoodAmountMultiplier += percent * (1f + _totemEfficiencyBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 식량생산량 +{percent * 100f:F0}% → 배율: {FoodAmountMultiplier:F2}x");
#endif
    }

    public void RemoveFoodAmountBuff(float percent)
    {
        FoodAmountMultiplier = Mathf.Max(0.1f, FoodAmountMultiplier - percent * (1f + _totemEfficiencyBonus));
    }

    // ── 식량 생산량 디버프 (마법사) ────────────────────────────
    public void AddFoodAmountDebuff(float reduction)
    {
        FoodAmountMultiplier = Mathf.Max(0.1f, FoodAmountMultiplier - reduction);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 식량생산량 -{reduction * 100f:F0}% → 배율: {FoodAmountMultiplier:F2}x");
#endif
    }

    public void RemoveFoodAmountDebuff(float reduction)
    {
        FoodAmountMultiplier = Mathf.Min(1f, FoodAmountMultiplier + reduction);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 식량생산량 -{reduction * 100f:F0}% 해제 → 배율: {FoodAmountMultiplier:F2}x");
#endif
    }

    // ── 전체 셀 버프 색상 플래그 재계산 ───────────────────────
    /// <summary>
    /// FindObjectsOfType 제거 — _activeTotem 목록으로 대체
    /// TotemBase.OnPlaced/OnRemoved 에서 호출
    /// </summary>
    public void RebuildCellBuffFlags()
    {
        if (Manager.Grid == null) return;

        // 1) 모든 셀 토템 버프 플래그 초기화 (기본 + 토템 전용 효과)
        foreach (var cell in Manager.Grid.AllCells())
        {
            cell.SetBuffFlags(false, false);
            cell.ClearTotemEffects();
        }

        // 2) 등록된 토템만 순회 (FindObjectsOfType 불필요)
        foreach (var totem in _activeTotem)
        {
            if (totem == null || !totem.IsActive) continue;
            totem.PaintAffectedCells();
        }
    }
}
