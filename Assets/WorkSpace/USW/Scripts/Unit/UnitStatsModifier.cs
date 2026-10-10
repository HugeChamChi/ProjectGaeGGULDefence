using UnityEngine;

public class UnitStatsModifier : MonoBehaviour
{
    private UnitBase _unit;
    private UnitDependencies _deps;
    private const int DefaultRows = 4;
    private const float ProjectileSizeStep = .1f;
    private int Rows => _deps?.GridManager?.Rows ?? DefaultRows;
    
    // 레벨업 스킬 특수 효과
    public float BurstEndTime { get; set; }

    public void Init(UnitBase unit, UnitDependencies deps)
    {
        _unit = unit;
        _deps = deps;
    }

    private float AtkUpgradeMultiplier => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetAtkUpgradeMultiplier(_unit.unitData.characterId.Get(_unit.currentTier))
        : 1f;

    private float AttackSpeedUpgradeMultiplier => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetAttackSpeedUpgradeMultiplier(_unit.unitData.characterId.Get(_unit.currentTier))
        : 1f;

    private float UpgradedAtk => _unit.unitData.atk.Get(_unit.currentTier) * AtkUpgradeMultiplier;

    // UnitData.attackSpeed is attacks per second; upgrades increase frequency.
    private float UpgradedAttackFrequency => (_unit.unitData != null ? _unit.unitData.attackSpeed.Get(_unit.currentTier) : 1.0f)
        * Mathf.Max(AttackSpeedUpgradeMultiplier, 0.01f);

    public int GetAttackDamage() => ComputeDamage(UpgradedAtk, 1f, true, out _, true);

    /// <summary>GetAttackDamage와 같고, 이번 추첨이 치명타였는지도 알려준다 (데미지 표시용).</summary>
    public int GetAttackDamage(out bool critical) => ComputeDamage(UpgradedAtk, 1f, true, out critical, true);

    /// <summary>현재 보정을 포함하되 치명타 추첨과 적 방어 계산을 하지 않는 표시용 공격력.</summary>
    public int GetNonCriticalAttackDamage() => ComputeDamage(UpgradedAtk, 1f, false, out _, true);

    /// <summary>보정(강화 등) 적용 후, 크리티컬/토템/부족 등 데미지 파이프라인 통과 전의 기준 공격력.</summary>
    public float GetUpgradedAtk() => UpgradedAtk;

    /// <summary>고정 기준 피해에 기존 보정(크리티컬/토템/부족/버스트)을 적용한다. 런 공격력 패널티는 제외한다.</summary>
    public int ComputeDamageFrom(float baseDamage) => ComputeDamage(baseDamage, 1f, true, out _);

    /// <summary>projAtkBonusMultiplier: "훈련의 성과" 류 투사체 크기 보너스 항목에만 추가로 곱해지는 배율(기본 1).</summary>
    public int ComputeDamageFrom(float baseDamage, float projAtkBonusMultiplier) => ComputeDamage(baseDamage, projAtkBonusMultiplier, true, out _);

    /// <summary>ComputeDamageFrom과 같고, 이번 추첨이 치명타였는지도 알려준다 (데미지 표시용).</summary>
    public int ComputeDamageFrom(float baseDamage, float projAtkBonusMultiplier, out bool critical)
        => ComputeDamage(baseDamage, projAtkBonusMultiplier, true, out critical);

    /// <summary>Attack-coefficient path; run attack applies once and fixed damage bypasses it.</summary>
    public int ComputeAttackDamageFrom(float baseDamage, float projAtkBonusMultiplier, out bool critical)
        => ComputeDamage(baseDamage, projAtkBonusMultiplier, true, out critical, true);

    private int ComputeDamage(float baseDamage, float projAtkBonusMultiplier, bool rollCritical, out bool critical, bool attackBased = false)
    {
        critical = false;
        if (_unit.unitData == null) return 0;

        float cellModifier = _unit.currentCell != null
            ? (_unit.currentCell.Model.NullifyDamageDebuff ? 1f : _unit.currentCell.Model.DamageModifier)
            : 1f;
        float totemModifier = _unit.currentCell?.Model.TotemAttackModifier ?? 1f;
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        var lu = _deps?.SelectionCombat;
        float rowModifier = lu?.GetRowAttackMultiplier(row, Rows) ?? 1f;

        float unitProjSizeMult = _unit.Buffs?.GetStatMultiplier(StatKind.ProjectileSize) ?? 1f;
        float projAtk = 1f + (GetProjectileSizeAtkBonus(unitProjSizeMult)) * projAtkBonusMultiplier;

        float burstAtk = (Time.time < BurstEndTime && lu != null)
            ? (1f + lu.BurstAttackBonus)
            : 1f;

        float cellAttackBonus = _unit.GetStatBonus(StatKind.AttackPercent, projAtkBonusMultiplier);
        float flatAttackBonus = _unit.GetStatBonus(StatKind.AttackFlat, projAtkBonusMultiplier);

        float damage = (baseDamage + flatAttackBonus)
                     * (1f + cellAttackBonus)
                     * cellModifier
                     * totemModifier
                     * rowModifier
                     * projAtk
                     * burstAtk;

        // 실제 타격만 편차를 준다 (표시용 GetNonCriticalAttackDamage는 rollCritical = false라 고정값).
        // 정수화/남은 HP 제한 전 평균은 보존한다. 반올림·막타·처치 타격 수는 달라질 수 있다.
        if (rollCritical) damage *= DamageCalculator.Spread(_deps?.CombatSettings?.DamageVariance ?? 0f);

        float cellCritChance = _unit.GetStatBonus(StatKind.CritChance);
        float critChance = (lu?.CritChance ?? 0f) + cellCritChance + (_deps?.Research?.Get(ResearchStat.CritChance) ?? 0f);
        if (rollCritical && UnityEngine.Random.value < critChance)
        {
            float critMultiplier = lu != null ? lu.CritDamageMultiplier : 1.5f;
            float cellCritDamage = _unit.GetStatBonus(StatKind.CritDamage);
            damage *= (critMultiplier + cellCritDamage + (_deps?.Research?.Get(ResearchStat.CritDamage) ?? 0f)) * DamageCalculator.Spread(_deps?.CombatSettings?.CritDamageVariance ?? 0f);
            critical = true;
        }

        if (attackBased) damage *= 1f + (_deps?.Research?.Get(ResearchStat.AttackPercent) ?? 0f);
        if (attackBased && _deps?.RunStatModifiers != null)
        {
            try { damage = RunStatMath.ScaleAttack(damage, _deps.RunStatModifiers.AttackMultiplier); }
            catch (System.OverflowException error)
            {
                _deps.ReportRunStatFailure?.Invoke(error.Message);
                return 0;
            }
        }
        return Mathf.Max(rollCritical ? 1 : 0, DamageCalculator.ApplyRounding(damage));
    }

    private float GetProjectileSizeAtkBonus(float unitSizeMultiplier)
    {
        var effects = _deps?.SelectionCombat;
        if (effects?.HasProjectileSizeScalesAtk != true) return 0f;
        float multiplier = (_deps?.TotemBuffManager?.ProjectileSizeMultiplier ?? 1f) * unitSizeMultiplier;
        return Mathf.Max(0f, (multiplier - 1f) / ProjectileSizeStep * effects.ProjectileSizeAtkPerUnit);
    }

    public float GetCurrentAttackInterval()
    {
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult   = Mathf.Max(_deps?.SelectionCombat?.GetRowSpeedMultiplier(row, Rows) ?? 1f, 0.01f);
        
        float baseInterval = 1.0f / Mathf.Max(UpgradedAttackFrequency, 0.01f);
        float cellSpeedBonusMult = 1f / Mathf.Max(0.1f, 1f + _unit.GetStatBonus(StatKind.Speed));

        float interval = baseInterval
                       * cellSpeedBonusMult
                       * (_unit.currentCell?.Model.SpeedModifier ?? 1f)
                       * (_unit.currentCell?.Model.TotemSpeedModifier ?? 1f)
                       / rowSpeedMult;
        interval /= 1f + (_deps?.Research?.Get(ResearchStat.AttackSpeedPercent) ?? 0f);
        float baseline = Mathf.Max(interval, 0.05f);
        try { return RunStatMath.ScaleAttackInterval(baseline, _deps?.RunStatModifiers?.AttackFrequencyMultiplier ?? 1d); }
        catch (System.OverflowException error)
        {
            _deps?.ReportRunStatFailure?.Invoke(error.Message);
            return float.PositiveInfinity; // Diagnostic sentinel; the progression owner stops/pauses the run.
        }
    }

    public float GetCurrentSkillInterval()
    {
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult = Mathf.Max(_deps?.SelectionCombat?.GetRowSpeedMultiplier(row, Rows) ?? 1f, 0.01f);
        float cellGaugeSpeedMult = 1f / Mathf.Max(0.1f, 1f + _unit.GetStatBonus(StatKind.GaugeSpeed));
        float interval = _unit.unitData.skillCooldown.Get(_unit.currentTier)
                       * _unit.SkillCooldownMultiplier
                       * cellGaugeSpeedMult
                       * (_unit.currentCell?.Model.SpeedModifier ?? 1f)
                       / rowSpeedMult;
        interval *= Mathf.Max(0f, 1f - (_deps?.Research?.Get(ResearchStat.SkillCooldownReduction) ?? 0f));
        return Mathf.Max(interval, 0.05f);
    }
}
