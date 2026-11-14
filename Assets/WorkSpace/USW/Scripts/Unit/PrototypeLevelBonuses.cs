using System.Collections.Generic;

/// <summary>Local fun-check milestones. Uses the existing combat effect contracts, without a server release.</summary>
public static class PrototypeLevelBonuses
{
    public static List<UnitLevelBonusDefinition> For(UnitBase unit)
    {
        if (unit is Disigman) return new()
        {
            S(3,"공격력 +5%",UnitBonusStat.AttackPercent,.05f),
            S(6,"회복 후 다음 일반공격 피해 +30%",UnitBonusStat.RecoveryNextBasicPercent,.3f),
            S(9,"공격속도 +5%",UnitBonusStat.BasicAttackFrequencyPercent,.05f),
            S(12,"시간 회복 +10%",UnitBonusStat.TimeRecoveryPercent,.1f),
            S(15,"일반공격 피해 +15%",UnitBonusStat.BasicDamagePercent,.15f)
        };
        if (unit is Drone_Betan) return new()
        {
            S(3,"공격력 +5%",UnitBonusStat.AttackPercent,.05f),
            Cycle(6,"매 2번째 기본 자폭 피해 +40%",.4f,1f),
            S(9,"공격속도 +5%",UnitBonusStat.BasicAttackFrequencyPercent,.05f),
            Cycle(12,"매 2번째 기본 자폭 충전 -30%",0f,.7f),
            S(15,"치명타 피해 +50%",UnitBonusStat.CriticalDamagePoints,.5f)
        };
        if (unit is Drone_Deltan) return new()
        {
            S(3,"공격속도 +10%",UnitBonusStat.BasicAttackFrequencyPercent,.1f),
            S(6,"충전 필요 공격 -1",UnitBonusStat.ChargeAttackReductionFlat,1f),
            S(9,"공유 해킹 최대 스택 +10",UnitBonusStat.HackingCapacityFlat,10f),
            S(12,"해킹 생산 +1",UnitBonusStat.HackingProducedNormal,1f),
            S(15,"해킹 후 2초간 공격속도 +15%",UnitBonusStat.HackingPostSpeedPercent,.15f,UnitBonusStat.HackingPostDuration,2f)
        };
        if (unit is Drone_Gamman) return new()
        {
            S(3,"공격력 +10%",UnitBonusStat.AttackPercent,.1f),
            S(6,"해킹 스택 피해 +20%",UnitBonusStat.HackingStackDamagePercent,.2f),
            S(9,"스킬 쿨타임 -10%",UnitBonusStat.SkillCooldownPercent,.1f),
            S(12,"해킹 소모 한도 +1",UnitBonusStat.HackingConsumedNormal,1f),
            S(15,"치명타 확률 +10%",UnitBonusStat.CriticalChancePoints,.1f)
        };
        return new()
        {
            S(3,"자체 생산속도 +10%",UnitBonusStat.FoodSpeedPercent,.1f),
            S(6,"보스 처치 시 자체 식량 5초분",UnitBonusStat.BossFoodSeconds,5f),
            S(9,"자체 생산량 +15%",UnitBonusStat.FoodPercent,.15f),
            S(12,"매 5번째 자체 생산 2배",UnitBonusStat.FoodFifthMultiplier,2f),
            S(15,"매 5번째 자체 생산 2.5배로 교체",UnitBonusStat.FoodFifthMultiplier,2.5f)
        };
    }
    private static UnitLevelBonusDefinition S(int level,string description,UnitBonusStat stat,float value,UnitBonusStat? second=null,float extra=0f)
    {
        var definition = new UnitLevelBonusDefinition { Level=level,Description=description };
        definition.Parts.Add(new StatModifierBonus { Stat=stat,Value=value });
        if (second.HasValue) definition.Parts.Add(new StatModifierBonus { Stat=second.Value,Value=extra });
        return definition;
    }
    private static UnitLevelBonusDefinition Cycle(int level,string description,float damage,float charge)
        => new() { Level=level,Description=description,Parts=new() { new EveryNthCycleBonus { CycleId=UnitLevelBonusController.BasicSkillCycle,Every=2,DamageBonusRatio=damage,ChargeTimeMultiplier=charge } } };
}
