using UnityEngine;

public class UnitStatsModifier : MonoBehaviour
{
    private UnitBase _unit;
    private UnitDependencies _deps;
    
    // 레벨업 스킬 특수 효과
    public float BurstEndTime { get; set; }
    public float UnemployedAtkBonus { get; set; }

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

    private float UpgradedAttackInterval => (_unit.unitData != null ? _unit.unitData.attackSpeed.Get(_unit.currentTier) : 1.0f)
        / Mathf.Max(AttackSpeedUpgradeMultiplier, 0.01f);

    public int GetAttackDamage() => ComputeDamage(UpgradedAtk);

    /// <summary>임의의 기준값을 GetAttackDamage()와 동일한 보정 파이프라인(크리티컬/토템/부족/버스트 등)에 통과시킨다.</summary>
    public int ComputeDamageFrom(float baseDamage) => ComputeDamage(baseDamage);

    /// <summary>projAtkBonusMultiplier: "훈련의 성과" 류 투사체 크기 보너스 항목에만 추가로 곱해지는 배율(기본 1).</summary>
    public int ComputeDamageFrom(float baseDamage, float projAtkBonusMultiplier) => ComputeDamage(baseDamage, projAtkBonusMultiplier);

    private int ComputeDamage(float baseDamage, float projAtkBonusMultiplier = 1f)
    {
        if (_unit.unitData == null) return 0;

        float cellModifier = _unit.currentCell != null
            ? (_unit.currentCell.Model.NullifyDamageDebuff ? 1f : _unit.currentCell.Model.DamageModifier)
            : 1f;
        float totemModifier = _unit.currentCell?.Model.TotemAttackModifier ?? 1f;
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        var lu = _deps?.LevelUpManager;
        float rowModifier = lu?.GetRowAttackMultiplier(row) ?? 1f;

        float tribeAtk = 1f + (lu?.GetTribeAtkBonus(_unit.unitData.unitTribe) ?? 0f);
        float unitProjSizeMult = _unit.Buffs?.GetStatMultiplier(StatKind.ProjectileSize) ?? 1f;
        float projAtk = 1f + (lu?.GetProjectileSizeAtkBonus(unitProjSizeMult) ?? 0f) * projAtkBonusMultiplier;

        float burstAtk = (Time.time < BurstEndTime && lu != null)
            ? (1f + lu.BurstAttackBonus)
            : 1f;

        float chieftainAtk = (_deps?.ChieftainManager?.ChieftainUnit == _unit && lu != null)
            ? 1f + lu.ChieftainAttackBonus
            : 1f;

        float globalPopPenalty = 1f;
        if (lu != null && lu.HasChieftainGainOnSell)
        {
            int excessPop = (_deps?.PopulationManager?.Current ?? 0) - 2;
            if (excessPop > 0)
            {
                globalPopPenalty -= excessPop * lu.ChieftainSellPopPenalty;
            }
        }
        globalPopPenalty = Mathf.Max(0.01f, globalPopPenalty);

        float cellAttackBonus = _unit.GetStatBonus(StatKind.AttackPercent, projAtkBonusMultiplier);
        float flatAttackBonus = _unit.GetStatBonus(StatKind.AttackFlat, projAtkBonusMultiplier);

        float damage = (baseDamage + UnemployedAtkBonus + flatAttackBonus)
                     * (1f + cellAttackBonus)
                     * cellModifier
                     * totemModifier
                     * rowModifier
                     * tribeAtk
                     * projAtk
                     * burstAtk
                     * chieftainAtk
                     * globalPopPenalty;

        float cellCritChance = _unit.GetStatBonus(StatKind.CritChance);
        float critChance = (lu?.CritChance ?? 0f) + cellCritChance;
        if (UnityEngine.Random.value < critChance)
        {
            float critMultiplier = lu != null ? lu.CritDamageMultiplier : 1.5f;
            float cellCritDamage = _unit.GetStatBonus(StatKind.CritDamage);
            damage *= (critMultiplier + cellCritDamage);
        }

        return Mathf.Max(1, DamageCalculator.ApplyRounding(damage));
    }

    public float GetCurrentAttackInterval()
    {
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult   = Mathf.Max(_deps?.LevelUpManager?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);
        float tribeSpeedMult = Mathf.Max(1f + (_deps?.LevelUpManager?.GetTribeSpeedBonus(_unit.unitData.unitTribe) ?? 0f), 0.01f);
        
        float baseInterval = 1.0f / Mathf.Max(UpgradedAttackInterval, 0.01f);
        float cellSpeedBonusMult = 1f / Mathf.Max(0.1f, 1f + _unit.GetStatBonus(StatKind.Speed));

        float interval = baseInterval
                       * cellSpeedBonusMult
                       * (_unit.currentCell?.Model.SpeedModifier ?? 1f)
                       * (_unit.currentCell?.Model.TotemSpeedModifier ?? 1f)
                       / rowSpeedMult
                       / tribeSpeedMult;
        return Mathf.Max(interval, 0.05f);
    }

    public float GetCurrentSkillInterval()
    {
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult = Mathf.Max(_deps?.LevelUpManager?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);
        float cellGaugeSpeedMult = 1f / Mathf.Max(0.1f, 1f + _unit.GetStatBonus(StatKind.GaugeSpeed));
        float interval = _unit.unitData.skillCooldown.Get(_unit.currentTier)
                       * cellGaugeSpeedMult
                       * (_unit.currentCell?.Model.SpeedModifier ?? 1f)
                       / rowSpeedMult;
        return Mathf.Max(interval, 0.05f);
    }
}
