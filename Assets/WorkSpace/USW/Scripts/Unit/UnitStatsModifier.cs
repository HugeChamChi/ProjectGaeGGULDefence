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

    private float UpgradedAtk => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetCurrentAtk(_unit.unitData.characterId)
        : _unit.unitData.atk;

    private float UpgradedAttackInterval => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetCurrentAttackSpeed(_unit.unitData.characterId)
        : (_unit.unitData != null ? _unit.unitData.attackSpeed : 1.0f);

    public int GetAttackDamage() => ComputeDamage(UpgradedAtk);
    public int GetSkillDamage()  => ComputeDamage(_unit.unitData.skillAtk);

    private int ComputeDamage(float baseDamage)
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
        float projAtk = 1f + (lu?.GetProjectileSizeAtkBonus() ?? 0f);

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

        float cellAttackBonus = _unit.currentCell?.Model.TotemCellAttackBonus ?? 0f;

        float damage = (baseDamage + UnemployedAtkBonus)
                     * ((_deps?.TotemBuffManager?.AttackMultiplier ?? 1f) + cellAttackBonus)
                     * cellModifier
                     * totemModifier
                     * rowModifier
                     * tribeAtk
                     * projAtk
                     * burstAtk
                     * chieftainAtk
                     * globalPopPenalty;

        float cellCritChance = _unit.currentCell?.Model.TotemCellCritChanceBonus ?? 0f;
        float critChance = (lu?.CritChance ?? 0f) + (_deps?.TotemBuffManager?.CritChanceBonus ?? 0f) + cellCritChance;
        if (UnityEngine.Random.value < critChance)
        {
            float critMultiplier = lu != null ? lu.CritDamageMultiplier : 1.5f;
            float cellCritDamage = _unit.currentCell?.Model.TotemCellCritDamageBonus ?? 0f;
            float totemCritDamage = _deps?.TotemBuffManager?.CritDamageBonus ?? 0f;
            damage *= (critMultiplier + totemCritDamage + cellCritDamage);
        }

        return Mathf.Max(1, DamageCalculator.ApplyRounding(damage));
    }

    public float GetCurrentAttackInterval()
    {
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult   = Mathf.Max(_deps?.LevelUpManager?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);
        float tribeSpeedMult = Mathf.Max(1f + (_deps?.LevelUpManager?.GetTribeSpeedBonus(_unit.unitData.unitTribe) ?? 0f), 0.01f);
        
        float baseInterval = 1.0f / Mathf.Max(UpgradedAttackInterval, 0.01f);
        float cellSpeedBonusMult = 1f / Mathf.Max(0.1f, 1f + (_unit.currentCell?.Model.TotemCellSpeedBonus ?? 0f));

        float interval = baseInterval
                       * (_deps?.TotemBuffManager?.SpeedMultiplier ?? 1f)
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
        float interval = _unit.unitData.skillCooldown
                       * (_deps?.TotemBuffManager?.GaugeSpeedMultiplier ?? 1f)
                       * (_unit.currentCell?.Model.SpeedModifier ?? 1f)
                       / rowSpeedMult;
        return Mathf.Max(interval, 0.05f);
    }
}
