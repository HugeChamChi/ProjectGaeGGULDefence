using UnityEngine;

public class UnitStatsModifier : MonoBehaviour
{
    private UnitBase _unit;
    private UnitDependencies _deps;
    private StatModifierProvider _provider;
    public StatModifierRegistry AdditionalModifiers { get; } = new StatModifierRegistry();
    private StatModifierSnapshot WithAdditional(StatModifierSnapshot snapshot) =>
        StatModifierSnapshot.Combine(snapshot, AdditionalModifiers.Capture());
    private void OnDisable() => AdditionalModifiers.Clear();
    public float BurstEndTime { get; set; }
    public void Init(UnitBase unit, UnitDependencies deps)
    {
        _unit = unit;
        _deps = deps;
        _provider = new StatModifierProvider(unit, deps);
        AdditionalModifiers.Clear();
    }
    private float BasicDamageMultiplier => UnitStatCalculator.BasicMultiplier(WithAdditional(_provider.CaptureBasic()));
    private float LevelBaseAtk => UnitStatCalculator.Attack(_unit.unitData.atk.Get(_unit.currentTier), WithAdditional(_provider.CaptureAttack()), false);
    private float UpgradedAtk => UnitStatCalculator.Attack(_unit.unitData.atk.Get(_unit.currentTier), WithAdditional(_provider.CaptureAttack()), true);

    public int GetAttackDamage() => ComputeDamage(UpgradedAtk, 1f, true, out _, true, BasicDamageMultiplier);

    /// <summary>GetAttackDamage와 같고, 이번 추첨이 치명타였는지도 알려준다 (데미지 표시용).</summary>
    public int GetAttackDamage(out bool critical) => ComputeDamage(UpgradedAtk, 1f, true, out critical, true, BasicDamageMultiplier);

    /// <summary>현재 보정을 포함하되 치명타 추첨과 적 방어 계산을 하지 않는 표시용 공격력.</summary>
    public int GetNonCriticalAttackDamage() => ComputeDamage(UpgradedAtk, 1f, false, out _, true);

    /// <summary>현재 런타임 보정을 유지하고 인게임 강화만 제외한 표시용 공격력.</summary>
    public int GetNonCriticalAttackDamageBeforeUpgrade() => ComputeDamage(LevelBaseAtk, 1f, false, out _, true);

    public int GetCombatDroneDamage(out bool critical) => ComputeDamage(UpgradedAtk * (_unit.unitData.CombatDroneAttackCoefficient + LevelDroneDamageCoefficient), 1f, true, out critical, true);
    public int GetCombatDroneDisplayDamage(bool includeUpgrade) => ComputeDamage(
        (includeUpgrade ? UpgradedAtk : LevelBaseAtk)
        * (_unit.unitData.CombatDroneAttackCoefficient + LevelDroneDamageCoefficient), 1f, false, out _, true);
    private float LevelDroneDamageCoefficient => _unit is Drone_Deltan deltan ? deltan.LevelBasicDamageCoefficient : 0f;

    /// <summary>보정(강화 등) 적용 후, 크리티컬/토템/부족 등 데미지 파이프라인 통과 전의 기준 공격력.</summary>
    public float GetUpgradedAtk() => UpgradedAtk;

    /// <summary>고정 기준 피해에 기존 보정(크리티컬/토템/부족/버스트)을 적용한다. 런 공격력 패널티는 제외한다.</summary>
    public int ComputeDamageFrom(float baseDamage) => ComputeDamage(baseDamage, 1f, true, out _);

    /// <summary>projAtkBonusMultiplier: "훈련의 성과" 류 투사체 크기 보너스 항목에만 추가로 곱해지는 배율(기본 1).</summary>
    public int ComputeDamageFrom(float baseDamage, float projAtkBonusMultiplier) => ComputeDamage(baseDamage, projAtkBonusMultiplier, true, out _);

    /// <summary>ComputeDamageFrom과 같고, 이번 추첨이 치명타였는지도 알려준다 (데미지 표시용).</summary>
    public int ComputeDamageFrom(float baseDamage, float projAtkBonusMultiplier, out bool critical, float damageMultiplier = 1f)
        => ComputeDamage(baseDamage, projAtkBonusMultiplier, true, out critical, damageMultiplier: damageMultiplier);

    /// <summary>Attack-coefficient path; run attack applies once and fixed damage bypasses it.</summary>
    public int ComputeAttackDamageFrom(float baseDamage, float projAtkBonusMultiplier, out bool critical, float damageMultiplier = 1f)
        => ComputeDamage(baseDamage, projAtkBonusMultiplier, true, out critical, true, damageMultiplier
            * (_unit.Combat?.IsExecutingRegularBasic == true ? BasicDamageMultiplier : 1f));

    private int ComputeDamage(float baseDamage, float projAtkBonusMultiplier, bool rollCritical, out bool critical, bool attackBased = false, float damageMultiplier = 1f)
    {
        critical = false;
        if (_unit.unitData == null) return 0;

        try
        {
            return UnitStatCalculator.Damage(baseDamage,
                WithAdditional(_provider.CaptureDamage(projAtkBonusMultiplier, BurstEndTime, attackBased, damageMultiplier)),
                rollCritical, attackBased, _deps?.CombatSettings?.DamageVariance ?? 0f,
                _deps?.CombatSettings?.CritDamageVariance ?? 0f, _deps?.RunStatModifiers?.AttackMultiplier ?? 1d,
                _deps?.RunStatModifiers != null, out critical);
        }
        catch (System.OverflowException error)
        {
            _deps?.ReportRunStatFailure?.Invoke(error.Message);
            return 0;
        }
    }

    public float GetCurrentAttackInterval() => GetCurrentAttackInterval(true);

    /// <summary>같은 런타임 보정에서 인게임 강화 포함 여부를 지정해 공격 간격을 계산한다.</summary>
    public float GetCurrentAttackInterval(bool includeUpgrade)
    {
        if (_unit.IsTimeVaultAttackSpeedActive && CappedAttackInterval() > 0f)
            return GetMinimumAttackInterval();

        try
        {
            return UnitStatCalculator.AttackInterval(_unit.unitData.attackSpeed.Get(_unit.currentTier),
                WithAdditional(_provider.CaptureIntervals()), includeUpgrade, _deps?.RunStatModifiers?.AttackFrequencyMultiplier ?? 1d,
                maximumSpeedMultiplier: _deps?.CombatSettings?.MaxAttackSpeedMultiplier ?? CombatSettings.DefaultMaxAttackSpeedMultiplier);
        }
        catch (System.OverflowException error)
        {
            _deps?.ReportRunStatFailure?.Invoke(error.Message);
            return float.PositiveInfinity;
        }
    }

    /// <summary>
    /// 공속 배율 상한이 허용하는 가장 짧은 공격 간격 (보스 감속 포함).
    /// 전투 드론처럼 본체 간격을 다시 나누는 경로가 같은 상한을 지키도록 공개한다. 기본 공속이 없으면 0.
    /// </summary>
    public float GetMinimumAttackInterval() => CappedAttackInterval() * BossSlowFactor();

    // The cap follows the current combat tier, including a separate temporary totem tier.
    private float CappedAttackInterval()
    {
        float baseFrequency = _unit.unitData != null ? _unit.unitData.attackSpeed.Get(_unit.currentTier) : 0f;
        if (!(baseFrequency > 0f)) return 0f;
        float cap = _deps?.CombatSettings?.MaxAttackSpeedMultiplier ?? CombatSettings.DefaultMaxAttackSpeedMultiplier;
        return 1f / (baseFrequency * cap);
    }

    // Cell slow from boss patterns (>1 slows) and the lizard pattern multiplier (<1 slows).
    private float BossSlowFactor() => Mathf.Max(1f, _unit.currentCell?.Model.SpeedModifier ?? 1f)
        / Mathf.Max(0.01f, _unit.PatternAttackSpeedMultiplier);

    public float GetCurrentSkillInterval() => UnitStatCalculator.SkillInterval(
        _unit.unitData.skillCooldown.Get(_unit.currentTier), WithAdditional(_provider.CaptureIntervals()));

    /// <summary>특정 효과의 표시 또는 실제 타격 상세. rollCritical=true는 실제 타격에서 한 번만 호출한다.</summary>
    public StatEvaluation EvaluateDamage(float baseDamage, bool attackBased, bool rollCritical = false,
        float passiveScale = 1f, float damageMultiplier = 1f)
    {
        var trace = new System.Collections.Generic.List<StatEvaluationStep>();
        var snapshot = WithAdditional(_provider.CaptureDamage(passiveScale, BurstEndTime, attackBased, damageMultiplier));
        try
        {
            int value = UnitStatCalculator.Damage(baseDamage, snapshot, rollCritical, attackBased,
                _deps?.CombatSettings?.DamageVariance ?? 0f, _deps?.CombatSettings?.CritDamageVariance ?? 0f,
                _deps?.RunStatModifiers?.AttackMultiplier ?? 1d, _deps?.RunStatModifiers != null, out bool critical, trace);
            return new StatEvaluation(value, critical, snapshot, trace);
        }
        catch (System.OverflowException error)
        {
            _deps?.ReportRunStatFailure?.Invoke(error.Message);
            return new StatEvaluation(0, false, snapshot, trace);
        }
    }

    /// <summary>정보창의 기존 기준 공격력과 동일한 계산 및 상세 내역. 난수를 소비하지 않는다.</summary>
    public int GetDisplayDamageDetails(System.Collections.Generic.List<StatEvaluationStep> trace, bool includeUpgrade = true, bool regularBasic = false)
    {
        float basis = UnitStatCalculator.Attack(_unit.unitData.atk.Get(_unit.currentTier), WithAdditional(_provider.CaptureAttack()), includeUpgrade, trace);
        try
        {
            return UnitStatCalculator.Damage(basis, WithAdditional(_provider.CaptureDamage(1f, BurstEndTime, true, regularBasic ? BasicDamageMultiplier : 1f)),
                false, true, 0f, 0f, _deps?.RunStatModifiers?.AttackMultiplier ?? 1d, _deps?.RunStatModifiers != null, out _, trace);
        }
        catch (System.OverflowException error)
        {
            _deps?.ReportRunStatFailure?.Invoke(error.Message);
            return 0;
        }
    }
}
