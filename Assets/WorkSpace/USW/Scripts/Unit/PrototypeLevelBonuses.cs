using System.Collections.Generic;

/// <summary>Local fun-check milestones. Uses the existing combat effect contracts, without a server release.</summary>
public static class PrototypeLevelBonuses
{
    public static List<UnitLevelBonusDefinition> For(UnitBase unit)
    {
        if (unit is Disigman) return new()
        {
            S(3,"공격속도 +10%",UnitBonusStat.BasicAttackFrequencyPercent,.1f),
            S(6,"스킬 피해 +20%",UnitBonusStat.SkillDamagePercent,.2f),
            S(9,"스킬 피해 +20%",UnitBonusStat.SkillDamagePercent,.2f),
            S(12,"공격속도 +10%",UnitBonusStat.BasicAttackFrequencyPercent,.1f),
            S(15,"스킬 충전 필요 일반공격 -1회 (6→5)",UnitBonusStat.ChargeAttackReductionFlat,1f)
        };
        if (unit is Drone_Betan) return new()
        {
            S(3,"공격력 +10%, 전투 드론 +1기",UnitBonusStat.AttackPercent,.1f,UnitBonusStat.DroneCountFlat,1f),
            S(6,"자폭 피해 +10%, 전투 드론 +1기",UnitBonusStat.BombDamagePercent,.1f,UnitBonusStat.DroneCountFlat,1f),
            S(9,"스킬 쿨타임 -15%, 전투 드론 +1기",UnitBonusStat.SkillCooldownPercent,.15f,UnitBonusStat.DroneCountFlat,1f),
            S(12,"자폭 피해 +15%, 전투 드론 +1기",UnitBonusStat.BombDamagePercent,.15f,UnitBonusStat.DroneCountFlat,1f),
            S(15,"매 3번째 기본 스킬 자폭 드론 +1기",UnitBonusStat.ThirdSkillExtraBomb,1f)
        };
        if (unit is Drone_Deltan) return new()
        {
            S(3,"공격속도 +5%",UnitBonusStat.BasicAttackFrequencyPercent,.05f),
            S(6,"공격 10회마다 추가 투사체 +1발",UnitBonusStat.ExtraProjectileCount,1f),
            S(9,"공격 10회마다 추가 투사체 +1발",UnitBonusStat.ExtraProjectileCount,1f),
            S(12,"공격 시 현재 해킹 스택당 공격력 0.5% 추가 피해",UnitBonusStat.HackingBasicDamagePerStack,.005f),
            S(15,"해킹 최대 스택 +15, 추가 투사체 주기 3회로 변경",UnitBonusStat.HackingCapacityFlat,15f)
        };
        if (unit is Drone_Gamman) return new()
        {
            S(3,"기폭 피해 +10%",UnitBonusStat.SkillDamagePercent,.1f),
            S(6,"기폭 쿨타임 -10%",UnitBonusStat.SkillCooldownPercent,.1f),
            S(9,"기폭 피해 +20%",UnitBonusStat.SkillDamagePercent,.2f),
            S(12,"기폭 쿨타임 -20%",UnitBonusStat.SkillCooldownPercent,.2f),
            S(15,"기폭으로 소비한 해킹 스택 20% 환급",UnitBonusStat.HackingRefundPercent,.2f)
        };
        return new()
        {
            S(3,"식량 생산량 +10%",UnitBonusStat.FoodPercent,.1f),
            S(6,"식량 생산량 +20%",UnitBonusStat.FoodPercent,.2f),
            S(9,"식량 생산속도 +10%",UnitBonusStat.FoodSpeedPercent,.1f),
            S(12,"식량 생산속도 +20%",UnitBonusStat.FoodSpeedPercent,.2f),
            S(15,"10초마다 50% 확률로 5초간 식량 생산량 2배",UnitBonusStat.FoodBurstChance,.5f)
        };
    }
    private static UnitLevelBonusDefinition S(int level,string description,UnitBonusStat stat,float value,UnitBonusStat? second=null,float extra=0f)
    {
        var definition = new UnitLevelBonusDefinition { Level=level,Description=description };
        definition.Parts.Add(new StatModifierBonus { Stat=stat,Value=value });
        if (second.HasValue) definition.Parts.Add(new StatModifierBonus { Stat=second.Value,Value=extra });
        return definition;
    }
}
