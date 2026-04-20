using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 토템 버프 수치 보관 + 전체 셀 버프 플래그 재계산
///
/// </summary>
public class TotemBuffManager : InGameSingleton<TotemBuffManager>
{
    // 공격력 배율 (기본 1.0)
    public float AttackMultiplier    { get; private set; } = 1f;
    // 속도 배율 (기본 1.0 — 낮을수록 게이지 빨라짐)
    public float SpeedMultiplier     { get; private set; } = 1f;
    // 식량 생산 속도 배율 (기본 1.0 — 낮을수록 식량 틱 빨라짐)
    public float FoodSpeedMultiplier { get; private set; } = 1f;
    // 식량 생산량 배율 (기본 1.0 — 고블린 마법사가 낮춤, 토템이 높임)
    public float FoodAmountMultiplier { get; private set; } = 1f;
    // 치명타 확률 보너스 (기본 0 — 토템으로 증가)
    public float CritChanceBonus      { get; private set; } = 0f;
    // 치명타 데미지 보너스 배율 (기본 0 — LevelUp 배율에 가산)
    public float CritDamageBonus      { get; private set; } = 0f;
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

    // ── 공격력 버프 ────────────────────────────────────────────
    public void AddAttackBuff(float percent)
    {
        AttackMultiplier += percent * (1f + _totemEfficiencyBonus);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 공격력 +{percent * 100f:F0}% → 배율: {AttackMultiplier:F2}x");
#endif
    }

    public void RemoveAttackBuff(float percent)
    {
        AttackMultiplier = Mathf.Max(1f, AttackMultiplier - percent * (1f + _totemEfficiencyBonus));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 공격력 -{percent * 100f:F0}% 해제 → 배율: {AttackMultiplier:F2}x");
#endif
    }

    // ── 속도 버프 ──────────────────────────────────────────────
    public void AddSpeedBuff(float percent)
    {
        SpeedMultiplier = Mathf.Max(0.1f, SpeedMultiplier - percent * (1f + _totemEfficiencyBonus));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 속도 +{percent * 100f:F0}% → 배율: {SpeedMultiplier:F2}x");
#endif
    }

    public void RemoveSpeedBuff(float percent)
    {
        SpeedMultiplier = Mathf.Min(1f, SpeedMultiplier + percent * (1f + _totemEfficiencyBonus));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 속도 -{percent * 100f:F0}% 해제 → 배율: {SpeedMultiplier:F2}x");
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
