using UnityEngine;

public class UnitStatsModifier : MonoBehaviour
{
    private UnitBase _unit;
    private UnitDependencies _deps;
    
    // 레벨업 스킬 특수 효과
    public float BurstEndTime { get; set; }
    public float UnemployedAtkBonus { get; set; }

    // 지원가(사제) 버프 — 다른 유닛이 부여하는 임시 공격력/공격속도 증폭
    private float _supportAtkBonus;
    private float _supportSpeedBonus;
    private float _supportBuffEndTime;

    public void ApplySupportBuff(float atkBonusPct, float speedBonusPct, float duration)
    {
        _supportAtkBonus = atkBonusPct;
        _supportSpeedBonus = speedBonusPct;
        _supportBuffEndTime = Time.time + duration;
    }

    private float SupportAtkMultiplier => Time.time < _supportBuffEndTime ? 1f + _supportAtkBonus : 1f;
    private float SupportSpeedMultiplier => Time.time < _supportBuffEndTime ? Mathf.Max(0.01f, 1f + _supportSpeedBonus) : 1f;

    public void Init(UnitBase unit, UnitDependencies deps)
    {
        _unit = unit;
        _deps = deps;
    }

    private float AtkUpgradeMultiplier => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetAtkUpgradeMultiplier(_unit.unitData.characterId)
        : 1f;

    private float AttackSpeedUpgradeMultiplier => _deps?.UpgradeManager != null && _deps.UpgradeManager.IsLoaded
        ? _deps.UpgradeManager.GetAttackSpeedUpgradeMultiplier(_unit.unitData.characterId)
        : 1f;

    private float UpgradedAtk => _unit.unitData.atk * AtkUpgradeMultiplier;

    private float UpgradedAttackInterval => (_unit.unitData != null ? _unit.unitData.attackSpeed : 1.0f)
        / Mathf.Max(AttackSpeedUpgradeMultiplier, 0.01f);

    public int GetAttackDamage() => ComputeDamage(UpgradedAtk);
    public int GetSkillDamage()  => ComputeDamage(_unit.unitData.skillAtk);

    /// <summary>패시브 공격력 보너스 배율을 적용해 스킬 데미지를 계산합니다.</summary>
    public int GetSkillDamage(float projAtkBonusMultiplier) => ComputeDamage(_unit.unitData.skillAtk, projAtkBonusMultiplier);

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
                     * globalPopPenalty
                     * SupportAtkMultiplier;

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
                       / tribeSpeedMult
                       / SupportSpeedMultiplier;
        return Mathf.Max(interval, 0.05f);
    }

    public float GetCurrentSkillInterval()
    {
        int row = _unit.currentCell?.GridPosition.y ?? 0;
        float rowSpeedMult = Mathf.Max(_deps?.LevelUpManager?.GetRowSpeedMultiplier(row) ?? 1f, 0.01f);
        float cellGaugeSpeedMult = 1f / Mathf.Max(0.1f, 1f + _unit.GetStatBonus(StatKind.GaugeSpeed));
        float interval = _unit.unitData.skillCooldown
                       * cellGaugeSpeedMult
                       * (_unit.currentCell?.Model.SpeedModifier ?? 1f)
                       / rowSpeedMult;
        return Mathf.Max(interval, 0.05f);
    }
}
