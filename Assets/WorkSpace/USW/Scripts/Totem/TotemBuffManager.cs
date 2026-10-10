using UnityEngine;
using VContainer;
using System.Collections.Generic;

/// <summary>
/// 토템 버프 수치 보관 + 전체 셀 버프 플래그 재계산
///
/// </summary>
public class TotemBuffManager : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;
    [Inject] private ResearchRunBonuses _research;
 
    public void Init()
    {
        if (_gridManager == null) _gridManager = _resolver.Resolve<GridManager>();
    }

    private GridManager _gridManager;

    // 활성 토템 목록 직접 관리 (FindObjectsOfType 대체)
    private readonly List<TotemBase> _activeTotem = new();
    private readonly Dictionary<GridCell, TotemBase> _attackDebuffSources = new Dictionary<GridCell, TotemBase>();

    /// <summary>겹친 토템 수와 무관하게 일반 공격 시도당 공유 아머 스택 하나만 부여한다.</summary>
    public void ApplyAttackDebuff(GridCell cell, BossBase boss)
    {
        if (cell == null || boss == null || !_attackDebuffSources.TryGetValue(cell, out var source) || source == null || !source.IsActive) return;
        if (source.TryGetDebuffBinding(out var binding)) boss.TryApplyDebuff(binding, source.GetInstanceID());
    }

    public event System.Action OnTotemBuffChanged;

    public int GetActiveTotemCount() => _activeTotem.Count;

    private SelectionEffectSnapshot _selectionEffects;

    /// <summary>선택지 기여만 교체한다. 다른 출처의 토템/판매/연구 보정은 보존하며 알림은 별도로 발행한다.</summary>
    public void ReplaceSelectionEffects(SelectionEffectSnapshot effects)
    {
        _selectionEffects = effects ?? throw new System.ArgumentNullException(nameof(effects));
        RebuildCellBuffFlags();
    }

    /// <summary>모든 지속 값의 반영이 끝난 뒤 선택지 조정자가 한 번 호출한다.</summary>
    public void NotifySelectionEffectsChanged()
    {
        if (OnTotemBuffChanged == null) return;
        foreach (System.Action handler in OnTotemBuffChanged.GetInvocationList())
            try { handler(); } catch (System.Exception error) { Debug.LogException(error); }
    }

    // ── 토템 효율 보너스 ──────────────────────────────────────────
    private float _totemEfficiencyBonus = 0f;
    public float TotemEfficiencyBonus => _totemEfficiencyBonus + (_selectionEffects?.TotemEfficiencyBonus ?? 0f) + (_research?.Get(ResearchStat.TotemEffect) ?? 0f);

    // ── 공격력 ───────────────────────────────────────────────────
    private float _totemAttackBonus   = 0f;
    private float _levelUpAttackBonus = 0f;
    public float AttackMultiplier => Mathf.Max(0.01f, 1f + (_totemAttackBonus * (1f + TotemEfficiencyBonus)) + (_levelUpAttackBonus + (_selectionEffects?.AttackBonus ?? 0f)));

    // ── 속도 (값이 클수록 초당 공격 횟수 증가, 즉 간격은 반비례) ────────────────
    private float _totemSpeedBonus   = 0f;
    private float _levelUpSpeedBonus = 0f;
    public float SpeedMultiplier => 1f / Mathf.Max(0.1f, 1f + (_totemSpeedBonus * (1f + TotemEfficiencyBonus)) + (_levelUpSpeedBonus + (_selectionEffects?.AttackSpeedBonus ?? 0f)));

    // ── 식량 생산 속도 (낮을수록 빠름) ────────────────────────────
    private float _totemFoodSpeedBonus = 0f;
    public float FoodSpeedMultiplier => 1f / Mathf.Max(0.1f, 1f + (_totemFoodSpeedBonus + (_selectionEffects?.FoodSpeedBonus ?? 0f)));

    // ── 식량 생산량 (고블린 마법사가 낮춤, 토템이 높임) ───────────
    private float _totemFoodAmountBonus = 0f;
    private float _debuffFoodAmount = 0f;
    public float FoodAmountMultiplier => Mathf.Max(0.1f, 1f + (_totemFoodAmountBonus * (1f + TotemEfficiencyBonus)) - _debuffFoodAmount);

    // ── 치명타 확률 ───────────────────────────────────────────────
    private float _totemCritChanceBonus = 0f;
    public float CritChanceBonus => _totemCritChanceBonus * (1f + TotemEfficiencyBonus);

    // ── 치명타 데미지 ─────────────────────────────────────────────
    private float _totemCritDamageBonus = 0f;
    public float CritDamageBonus => _totemCritDamageBonus * (1f + TotemEfficiencyBonus);

    // ── 게이지 회복 속도 (낮을수록 빠름) ──────────────────────────
    private float _totemGaugeSpeedBonus = 0f;
    public float GaugeSpeedMultiplier => 1f / Mathf.Max(0.1f, 1f + (_totemGaugeSpeedBonus + (_selectionEffects?.GaugeSpeedBonus ?? 0f)));

    // ── 투사체 크기 ───────────────────────────────────────────────
    private float _totemProjectileSizeBonus = 0f;
    public float ProjectileSizeMultiplier => Mathf.Max(0.1f, 1f + (_totemProjectileSizeBonus + (_selectionEffects?.ProjectileSizeBonus ?? 0f)));

    /// <summary>kind에 해당하는 토템/레벨업 전역 가산 보너스를 반환한다.</summary>
    public float GetGlobalStatBonus(StatKind kind)
    {
        switch (kind)
        {
            case StatKind.AttackPercent: return _totemAttackBonus * (1f + TotemEfficiencyBonus) + (_levelUpAttackBonus + (_selectionEffects?.AttackBonus ?? 0f));
            case StatKind.Speed: return _totemSpeedBonus * (1f + TotemEfficiencyBonus) + (_levelUpSpeedBonus + (_selectionEffects?.AttackSpeedBonus ?? 0f));
            case StatKind.FoodSpeed: return (_totemFoodSpeedBonus + (_selectionEffects?.FoodSpeedBonus ?? 0f));
            case StatKind.FoodAmount: return _totemFoodAmountBonus * (1f + TotemEfficiencyBonus) - _debuffFoodAmount;
            case StatKind.CritChance: return _totemCritChanceBonus * (1f + TotemEfficiencyBonus);
            case StatKind.CritDamage: return _totemCritDamageBonus * (1f + TotemEfficiencyBonus);
            case StatKind.GaugeSpeed: return (_totemGaugeSpeedBonus + (_selectionEffects?.GaugeSpeedBonus ?? 0f));
            case StatKind.ProjectileSize: return (_totemProjectileSizeBonus + (_selectionEffects?.ProjectileSizeBonus ?? 0f));
            default: return 0f;
        }
    }

    // ── 토템 등록/해제 ─────────────────────────────────────────

    public void RegisterTotem(TotemBase totem)
    {
        if (!_activeTotem.Contains(totem))
        {
            _activeTotem.Add(totem);
            OnTotemBuffChanged?.Invoke();
        }
    }

    public void UnregisterTotem(TotemBase totem)
    {
        if (_activeTotem.Remove(totem))
        {
            OnTotemBuffChanged?.Invoke();
        }
    }

    // ── 공격력 버프 (토템 전용) ───────────────
    public void AddAttackBuff(float amount)
    {
        _totemAttackBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    public void RemoveAttackBuff(float amount)
    {
        _totemAttackBonus = Mathf.Max(0f, _totemAttackBonus - amount);
        OnTotemBuffChanged?.Invoke();
    }

    // ── 공격력 버프 (레벨업/판매 전용) ──────
    public void AddLevelUpAttackBuff(float percent)
    {
        _levelUpAttackBonus += percent;
        OnTotemBuffChanged?.Invoke();
    }

    // ── 속도 버프 (토템 전용) ─────────────────
    public void AddSpeedBuff(float amount)
    {
        _totemSpeedBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    public void RemoveSpeedBuff(float amount)
    {
        _totemSpeedBonus = Mathf.Max(0f, _totemSpeedBonus - amount);
        OnTotemBuffChanged?.Invoke();
    }

    // ── 속도 버프 (레벨업 전용) ─────────────
    public void AddLevelUpSpeedBuff(float percent)
    {
        _levelUpSpeedBonus += percent;
        OnTotemBuffChanged?.Invoke();
    }

    // ── 토템 효율 ──────────────────────────────────────────────
    public void AddTotemEfficiency(float bonus)
    {
        _totemEfficiencyBonus += bonus;
        RebuildCellBuffFlags();
        OnTotemBuffChanged?.Invoke();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBuff] 토템 효율 +{bonus * 100f:F0}% → 보너스: {_totemEfficiencyBonus:F2}");
#endif
    }

    // ── 식량 속도 버프 ─────────────────────────────────────────
    public void AddFoodSpeedBuff(float amount)
    {
        _totemFoodSpeedBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    public void RemoveFoodSpeedBuff(float amount)
    {
        _totemFoodSpeedBonus -= amount;
        OnTotemBuffChanged?.Invoke();
    }

    // ── 치명타 버프 (토템) ─────────────────────────────────────
    public void AddCritChanceBuff(float amount)
    {
        _totemCritChanceBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    public void RemoveCritChanceBuff(float amount)
    {
        _totemCritChanceBonus = Mathf.Max(0f, _totemCritChanceBonus - amount);
        OnTotemBuffChanged?.Invoke();
    }

    public void AddCritDamageBuff(float amount)
    {
        _totemCritDamageBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    public void RemoveCritDamageBuff(float amount)
    {
        _totemCritDamageBonus = Mathf.Max(0f, _totemCritDamageBonus - amount);
        OnTotemBuffChanged?.Invoke();
    }

    // ── 게이지 회복 속도 버프 ──────────────────────────────────
    public void AddGaugeSpeedBuff(float amount)
    {
        _totemGaugeSpeedBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    // ── 투사체 크기 버프 ───────────────────────────────────────
    public void AddProjectileSizeBuff(float amount)
    {
        _totemProjectileSizeBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    // ── 식량 생산량 버프 (토템) ────────────────────────────────
    public void AddFoodAmountBuff(float amount)
    {
        _totemFoodAmountBonus += amount;
        OnTotemBuffChanged?.Invoke();
    }

    public void RemoveFoodAmountBuff(float amount)
    {
        _totemFoodAmountBonus = Mathf.Max(0f, _totemFoodAmountBonus - amount);
        OnTotemBuffChanged?.Invoke();
    }

    // ── 식량 생산량 디버프 (마법사) ────────────────────────────
    public void AddFoodAmountDebuff(float reductionAmount)
    {
        _debuffFoodAmount += reductionAmount;
        OnTotemBuffChanged?.Invoke();
    }

    public void RemoveFoodAmountDebuff(float reductionAmount)
    {
        _debuffFoodAmount = Mathf.Max(0f, _debuffFoodAmount - reductionAmount);
        OnTotemBuffChanged?.Invoke();
    }

    // ── 전체 셀 버프 색상 플래그 재계산 ───────────────────────
    public void RebuildCellBuffFlags()
    {
        _attackDebuffSources.Clear();
        if (_gridManager == null) return;

        // 1) 모든 셀 토템 버프 플래그 초기화 (기본 + 토템 전용 효과)
        foreach (var cell in _gridManager.AllCells())
        {
            if (cell != null)
            {
                cell.SetBuffFlags(false, false);
                cell.ClearTotemEffects();
            }
        }

        // 2) 등록된 토템만 순회
        foreach (var totem in _activeTotem)
        {
            if (totem == null || !totem.IsActive) continue;
            totem.PaintAffectedCells();
            if (totem.TryGetDebuffBinding(out var binding) && binding.Trigger == DebuffTrigger.AffectedUnitBasicAttackAttempt)
                foreach (var cell in totem.GetAffectedCells())
                    if (cell != null && !_attackDebuffSources.ContainsKey(cell)) _attackDebuffSources.Add(cell, totem);
        }
    }
}

