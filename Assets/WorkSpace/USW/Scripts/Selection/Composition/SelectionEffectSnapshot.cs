using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>활성 카드의 불변 계산 결과. 생성 후 쓰기 API나 가변 효과 정의를 노출하지 않는다.</summary>
public sealed class SelectionEffectSnapshot : ISelectionCombatReader, IDroneEffectReader, ISelectionEconomyReader, IChiefSelectionReader
{
    /// <summary>감망 운반 자폭 프리팹.</summary>
    public SelfDestructDrone GammanBombPrefab { get; private set; }
    /// <summary>BasicSkillExtraBombs 선택지 기여.</summary>
    public int BasicSkillExtraBombs { get; private set; } = 0;
    /// <summary>BombRearmHits 선택지 기여.</summary>
    public int BombRearmHits { get; private set; } = 0;
    /// <summary>BombRearmExtraBombs 선택지 기여.</summary>
    public int BombRearmExtraBombs { get; private set; } = 0;
    /// <summary>BombFuseStacks 선택지 기여.</summary>
    public int BombFuseStacks { get; private set; } = 0;
    /// <summary>BombFuseBonusPerStack 선택지 기여.</summary>
    public float BombFuseBonusPerStack { get; private set; } = 0;
    /// <summary>HasGammanBomb 선택지 기여.</summary>
    public bool HasGammanBomb { get; private set; } = false;
    /// <summary>BetanCycleMultiplier 선택지 기여.</summary>
    public float BetanCycleMultiplier { get; private set; } = 1f;
    /// <summary>HasCounterBomb 선택지 기여.</summary>
    public bool HasCounterBomb { get; private set; } = false;
    /// <summary>BombDamageMultiplier 선택지 기여.</summary>
    public float BombDamageMultiplier { get; private set; } = 1f;
    /// <summary>BombWarheadCycleMultiplier 선택지 기여.</summary>
    public float BombWarheadCycleMultiplier { get; private set; } = 1f;
    /// <summary>BombArmorReduction 선택지 기여.</summary>
    public float BombArmorReduction { get; private set; } = 0;
    /// <summary>BombArmorDuration 선택지 기여.</summary>
    public float BombArmorDuration { get; private set; } = 0;
    /// <summary>BombExecutionThreshold 선택지 기여.</summary>
    public float BombExecutionThreshold { get; private set; } = 0;
    /// <summary>BombExecutionBonus 선택지 기여.</summary>
    public float BombExecutionBonus { get; private set; } = 0;
    /// <summary>BombSalvageHits 선택지 기여.</summary>
    public int BombSalvageHits { get; private set; } = 0;
    /// <summary>BombSalvageFood 선택지 기여.</summary>
    public float BombSalvageFood { get; private set; } = 0;

    /// <summary>HackingOverflowAttackCoefficient 선택지 기여.</summary>
    public float HackingOverflowAttackCoefficient { get; private set; } = 0f;
    /// <summary>HackingConsumptionMultiplier 선택지 기여.</summary>
    public float HackingConsumptionMultiplier { get; private set; } = 1f;
    /// <summary>HackingStackDamageMultiplier 선택지 기여.</summary>
    public float HackingStackDamageMultiplier { get; private set; } = 1f;
    /// <summary>HackingCapacityBonus 선택지 기여.</summary>
    public int HackingCapacityBonus { get; private set; } = 0;
    /// <summary>BetanAttackReductionRatio 선택지 기여.</summary>
    public float BetanAttackReductionRatio { get; private set; } = 0f;
    /// <summary>AttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float AttackBonus { get; private set; } = 0f;
    /// <summary>AttackSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float AttackSpeedBonus { get; private set; } = 0f;
    /// <summary>TotemEfficiencyBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float TotemEfficiencyBonus { get; private set; } = 0f;
    /// <summary>CritChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float CritChance { get; private set; } = 0f;
    /// <summary>CritDamageMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float CritDamageMultiplier { get; private set; } = CombatSettings.DefaultBaseCritDamageMultiplier;
    /// <summary>FoodSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FoodSpeedBonus { get; private set; } = 0f;
    /// <summary>ProjectileSizeBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ProjectileSizeBonus { get; private set; } = 0f;
    /// <summary>GaugeSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float GaugeSpeedBonus { get; private set; } = 0f;
    /// <summary>ExpGainMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ExpGainMultiplier { get; private set; } = 1f;
    /// <summary>FrontAttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FrontAttackBonus { get; private set; } = 0f;
    /// <summary>BackAttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BackAttackBonus { get; private set; } = 0f;
    /// <summary>FrontSpeedMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FrontSpeedMultiplier { get; private set; } = 1f;
    /// <summary>BackSpeedMultiplier의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BackSpeedMultiplier { get; private set; } = 1f;
    /// <summary>RandomExtraAttackChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RandomExtraAttackChance { get; private set; } = 0f;
    /// <summary>HasRandomProcAttack의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasRandomProcAttack { get; private set; } = false;
    /// <summary>RandomProcChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RandomProcChance { get; private set; } = 0f;
    /// <summary>RandomProcDamagePct의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RandomProcDamagePct { get; private set; } = 0f;
    /// <summary>HasExtraAttackEveryAttack의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasExtraAttackEveryAttack { get; private set; } = false;
    /// <summary>HasExtraAttackOnSkillFull의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasExtraAttackOnSkillFull { get; private set; } = false;
    /// <summary>HasBurstOnSkillFull의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasBurstOnSkillFull { get; private set; } = false;
    /// <summary>BurstAttackBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BurstAttackBonus { get; private set; } = 0f;
    /// <summary>BurstDurationSeconds의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BurstDurationSeconds { get; private set; } = 0f;
    /// <summary>SummonDiscountRate의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SummonDiscountRate { get; private set; } = 0f;
    /// <summary>SummonFixedDiscountAmount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SummonFixedDiscountAmount { get; private set; } = 0f;
    /// <summary>SellBonusFoodAmount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SellBonusFoodAmount { get; private set; } = 0f;
    /// <summary>HasSummonDealsDamage의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasSummonDealsDamage { get; private set; } = false;
    /// <summary>SummonDamagePct의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SummonDamagePct { get; private set; } = 0f;
    /// <summary>HasSellDealsDamage의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasSellDealsDamage { get; private set; } = false;
    /// <summary>SellDamagePct의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SellDamagePct { get; private set; } = 0f;
    /// <summary>HasSellGivesRandomUnit의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasSellGivesRandomUnit { get; private set; } = false;
    /// <summary>SellGivesUnitChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float SellGivesUnitChance { get; private set; } = 0f;
    /// <summary>HasMergeKeepsTribe의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasMergeKeepsTribe { get; private set; } = false;
    /// <summary>HasAllowTotemOverlap의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasAllowTotemOverlap { get; private set; } = false;
    /// <summary>HasProjectileSizeScalesAtk의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasProjectileSizeScalesAtk { get; private set; } = false;
    /// <summary>ProjectileSizeAtkPerUnit의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ProjectileSizeAtkPerUnit { get; private set; } = 0f;
    /// <summary>PeriodicBombInterval의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float PeriodicBombInterval { get; private set; } = 0f;
    /// <summary>PeriodicBombCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int PeriodicBombCount { get; private set; } = 0;
    /// <summary>FleetBombInterval의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FleetBombInterval { get; private set; } = 0f;
    /// <summary>FleetBombCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int FleetBombCount { get; private set; } = 0;
    /// <summary>DroneAttackSpeedBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float DroneAttackSpeedBonus { get; private set; } = 0f;
    /// <summary>전투 드론 1기당 감망 스택 추가 피해 증가 비율.</summary>
    public float StackDamageBonusPerDrone { get; private set; } = 0f;
    /// <summary>스택 추가 피해 증가 상한. 0이면 무상한.</summary>
    public float MaximumStackDamageBonus { get; private set; } = 0f;
    /// <summary>HasEmptyStackDamage의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasEmptyStackDamage { get; private set; } = false;
    /// <summary>젤탕 자체·군단 생산을 합쳐 누적 지급하는 경제 정책의 활성 여부.</summary>
    public bool HasBatchedFoodPayout { get; private set; } = false;
    /// <summary>FoodPayoutInterval의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FoodPayoutInterval { get; private set; } = 1f;
    /// <summary>FoodProductionBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float FoodProductionBonus { get; private set; } = 0f;
    /// <summary>WaveFoodDividendRatio의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float WaveFoodDividendRatio { get; private set; } = 0f;
    /// <summary>HackingProductionFlat의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int HackingProductionFlat { get; private set; } = 0;
    /// <summary>HackingProductionBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float HackingProductionBonus { get; private set; } = 0f;
    /// <summary>압축 업로드의 기본 충전량. 0이면 미획득.</summary>
    public int HackingCompressionRequiredAttacks { get; private set; }
    /// <summary>압축 업로드 생산 배수.</summary>
    public int HackingCompressionMultiplier { get; private set; } = 1;
    /// <summary>ManaRequirementReduction의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ManaRequirementReduction { get; private set; } = 0f;
    /// <summary>마나 감소 기능의 활성 여부.</summary>
    public bool HasManaRequirementReduction { get; private set; }
    /// <summary>델탕 해킹 생산량 배수 발동 확률.</summary>
    public float HackingProductionProcChance { get; private set; } = 0f;
    /// <summary>발동 시 생산량 배수.</summary>
    public int HackingProductionProcMultiplier { get; private set; } = 1;
    /// <summary>감망 기폭 여운 활성 여부.</summary>
    public bool HasGammanResidue { get; private set; }
    /// <summary>여운 총량 비율. 1=100%.</summary>
    public float GammanResidueRatio { get; private set; } = 0f;
    /// <summary>여운 지속 시간(초).</summary>
    public float GammanResidueDuration { get; private set; } = 0f;
    /// <summary>여운 틱 수.</summary>
    public int GammanResidueTicks { get; private set; } = 0;
    /// <summary>ChiefCooldownReduction의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ChiefCooldownReduction { get; private set; } = 0f;
    /// <summary>족장 스킬의 최대 순차 충전 보관 횟수.</summary>
    public int ChiefChargeCapacity { get; private set; } = 1;
    /// <summary>긴급 교신의 소모 스택당 집결 추가 피해 비율.</summary>
    public float ChiefHackingDamagePerStack { get; private set; }
    /// <summary>ChiefVolleyDelay의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ChiefVolleyDelay { get; private set; } = 0f;
    /// <summary>ChiefVolleyDamageRatio의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ChiefVolleyDamageRatio { get; private set; } = 0f;
    /// <summary>ChiefVolleyCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int ChiefVolleyCount { get; private set; } = 0;
    /// <summary>RallyDamageBonus의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float RallyDamageBonus { get; private set; } = 0f;
    /// <summary>집결 피해 배율 정의의 활성 여부. 기존 0배 저작도 보존한다.</summary>
    public bool HasRallyDamage { get; private set; }
    /// <summary>ExplosionSkillRecoverySeconds의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float ExplosionSkillRecoverySeconds { get; private set; } = 0f;
    /// <summary>MergeSupportChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float MergeSupportChance { get; private set; } = 0f;
    /// <summary>ExtraCombatDroneCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int ExtraCombatDroneCount { get; private set; } = 0;
    /// <summary>OverflowManaPerCast의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int OverflowManaPerCast { get; private set; } = 0;
    /// <summary>HasBombHacking의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public bool HasBombHacking { get; private set; } = false;
    /// <summary>HackingStacksPerBomb의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int HackingStacksPerBomb { get; private set; } = 0;
    /// <summary>HackingCarryoverRatio의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float HackingCarryoverRatio { get; private set; } = 0f;
    /// <summary>BonusManaChance의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public float BonusManaChance { get; private set; } = 0f;
    /// <summary>BonusManaCount의 현재 선택지 기여. 비율은 1=100%, 시간은 초 단위.</summary>
    public int BonusManaCount { get; private set; } = 0;
    /// <summary>과부하 합성 획득 여부.</summary>
    /// <summary>가속 운전의 추가 타이머 감소율.</summary>
    public float ExtraTimeDrainRatio { get; private set; }
    /// <summary>가속 운전의 공속 가산.</summary>
    public float AcceleratedAttackSpeedBonus { get; private set; }
    /// <summary>제물 지출 초.</summary>
    public float SacrificeCostSeconds { get; private set; }
    /// <summary>제물 지출당 공격력 가산.</summary>
    public float SacrificeAttackPerPayment { get; private set; }
    /// <summary>비상 전력 유예 초.</summary>
    public float TimeVaultSeconds { get; private set; }
    /// <summary>과열당 초과 스택 수.</summary>
    public int OverheatOverflowPerStack { get; private set; }
    /// <summary>과열당 지출 초.</summary>
    public float OverheatCostSeconds { get; private set; }
    /// <summary>과열의 일반 스택 대비 피해 가중치.</summary>
    public float OverheatDamageWeight { get; private set; }
    public bool HasOverloadMerge { get; private set; }
    /// <summary>과부하 합성 시간 비용.</summary>
    public float OverloadMergeCostSeconds { get; private set; }
    /// <summary>과부하 합성 추가 승급 확률.</summary>
    public float OverloadMergeUpgradeChance { get; private set; }
    /// <summary>복구 패킷의 회복당 스택.</summary>
    public int RecoveryHackingStacks { get; private set; }
    /// <summary>복구 패킷 공유 해킹 설정.</summary>
    public DroneHackingData RecoveryHackingData { get; private set; }
    /// <summary>소각 신관 공유 자폭 프리팹.</summary>
    public SelfDestructDrone GlitchBombPrefab { get; private set; }
    /// <summary>오류 누적 1회당 가산 비율.</summary>
    public float GlitchAttackPerTrigger { get; private set; }
    /// <summary>오류 누적 최대 횟수.</summary>
    public int GlitchAttackMaximumTriggers { get; private set; }
    private readonly List<int> _hitPeriods = new();
    private readonly Dictionary<int,float> _upgradeDiscounts = new();
    private readonly HashSet<Type> _singleConfigurations = new();
    /// <summary>공격 횟수 트리거의 읽기 전용 목록.</summary>
    public IReadOnlyList<int> BonusAttackEveryNHits { get; }
    /// <summary>출처별 강화 할인. 환급 이력은 포함하지 않는다.</summary>
    public IReadOnlyDictionary<int,float> UpgradeDiscounts { get; }
    internal SelectionEffectSnapshot(IEnumerable<SelectionCardSnapshot> cards, float baseCritChance, float baseCritDamageMultiplier = CombatSettings.DefaultBaseCritDamageMultiplier)
    {
        Finite(baseCritChance); CritChance=Mathf.Clamp01(baseCritChance);
        Finite(baseCritDamageMultiplier); CritDamageMultiplier=Mathf.Max(1f, baseCritDamageMultiplier);
        BonusAttackEveryNHits=_hitPeriods.AsReadOnly();
        UpgradeDiscounts=new ReadOnlyDictionary<int,float>(_upgradeDiscounts);
        var ids=new HashSet<int>(); var groups=new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            if (card == null || !ids.Add(card.CardId)) throw new ArgumentException("Null or duplicate active card.");
            foreach (var group in card.ExclusiveGroups) if (!groups.Add(group)) throw new ArgumentException("Exclusive selection group conflict: " + group);
            foreach (var effect in card.CopyDefinition().Effects)
            {
                if (RequiresSingleConfiguration(effect)) Once(effect);
                switch (effect)
                {
                    case StatModifierDefinition d: ApplyStat(d); break;
                    case UpgradeDiscountDefinition d:
                        if (_upgradeDiscounts.ContainsKey(card.CardId)) throw new ArgumentException("Multiple upgrade discounts on one card.");
                        _upgradeDiscounts.Add(card.CardId,d.Ratio); break;
                    case BasicBombCountDefinition d: BasicSkillExtraBombs=d.ExtraBombs; break;
                    case BombRearmDefinition d: BombRearmHits=d.HitsRequired; BombRearmExtraBombs=d.ExtraBombs; break;
                    case BombStackFuseDefinition d: BombFuseStacks=d.MaximumStacks; BombFuseBonusPerStack=d.BonusPerStack; break;
                    case GammanBombDefinition d: HasGammanBomb=true; GammanBombPrefab=d.Prefab; break;
                    case BetanCycleDefinition d: BetanCycleMultiplier=d.TimeMultiplier; break;
                    case CounterBombDefinition d: HasCounterBomb=true; break;
                    case BombWarheadDefinition d: BombDamageMultiplier=d.DamageMultiplier; BombWarheadCycleMultiplier=d.CycleMultiplier; break;
                    case BombArmorDefinition d: BombArmorReduction=d.ReductionRatio; BombArmorDuration=d.DurationSeconds; break;
                    case BombExecutionDefinition d: BombExecutionThreshold=d.HpThreshold; BombExecutionBonus=d.DamageBonus; break;
                    case BombSalvageDefinition d: BombSalvageHits=d.HitsRequired; BombSalvageFood=d.FoodAmount; break;
                    case PeriodicBombDefinition d: PeriodicBombInterval=d.PeriodSeconds; PeriodicBombCount=d.BombCount; break;
                    case DroneAttackSpeedDefinition d: DroneAttackSpeedBonus+=d.BonusRatio; break;
                    case FleetBombDefinition d: FleetBombInterval=d.PeriodSeconds; FleetBombCount=d.BombCount; break;
                    case StackSkillFrequencyDefinition d: StackDamageBonusPerDrone=d.ReductionPerStack; MaximumStackDamageBonus=d.MaximumReduction; break;
                    case EmptyStackDamageDefinition d: HasEmptyStackDamage=true; break;
                    case FoodPayoutDefinition d: HasBatchedFoodPayout=true; FoodPayoutInterval=d.PeriodSeconds; FoodProductionBonus+=d.BonusRatio; break;
                    case HackingCompressionDefinition d: HackingCompressionRequiredAttacks=d.RequiredAttacks; HackingCompressionMultiplier=d.ProductionMultiplier; break;
                    case HackingProductionFlatDefinition d: HackingProductionFlat=checked(HackingProductionFlat+d.StackCount); break;
                    case HackingProductionRollDefinition d: if (d.MinimumRatio != d.MaximumRatio) throw new ArgumentException("Production roll must be resolved by Prepare."); HackingProductionBonus+=d.MinimumRatio; break;
                    case ManaRequirementDefinition d: HasManaRequirementReduction=true; ManaRequirementReduction=d.ReductionRatio; break;
                    case HackingProductionProcDefinition d: HackingProductionProcChance=d.Chance; HackingProductionProcMultiplier=d.Multiplier; break;
                    case GammanResidueDefinition d: HasGammanResidue=true; GammanResidueRatio=d.DamageRatio; GammanResidueDuration=d.DurationSeconds; GammanResidueTicks=d.TickCount; break;
                    case ChiefCooldownDefinition d: ChiefCooldownReduction+=d.ReductionRatio; break;
                    case ChiefChargeCapacityDefinition d: ChiefChargeCapacity=d.MaximumCharges; break;
                    case ChiefHackingDetonationDefinition d: ChiefHackingDamagePerStack=d.DamageRatioPerStack; break;
                    case ChiefVolleyDefinition d: ChiefVolleyDelay=d.ShotDelaySeconds; ChiefVolleyDamageRatio=d.DamageRatio; ChiefVolleyCount=d.ShotCount; break;
                    case FoodProductionDefinition d: FoodProductionBonus+=d.BonusRatio; break;
                    case WaveFoodDividendDefinition d: WaveFoodDividendRatio=d.Ratio; break;
                    case RallyDamageDefinition d: HasRallyDamage=true; RallyDamageBonus+=d.BonusRatio; break;
                    case ExplosionSkillRecoveryDefinition d: ExplosionSkillRecoverySeconds+=d.Seconds; break;
                    case MergeSupportDefinition d: MergeSupportChance+=d.Chance; break;
                    case CombatDroneCapacityDefinition d: ExtraCombatDroneCount=checked(ExtraCombatDroneCount+d.Count); break;
                    case OverflowManaDefinition d: OverflowManaPerCast=d.ManaPerCast; break;
                    case HackingOverflowDamageDefinition d: HackingOverflowAttackCoefficient+=d.AttackCoefficient; break;
                    case HackingConsumptionDefinition d: HackingConsumptionMultiplier*=d.ConsumptionMultiplier; HackingStackDamageMultiplier*=d.StackDamageMultiplier; break;
                    case HackingCapacityDefinition d: HackingCapacityBonus=checked(HackingCapacityBonus+d.AdditionalCapacity); break;
                    case BetanAttackPenaltyDefinition d: BetanAttackReductionRatio=1f-(1f-BetanAttackReductionRatio)*(1f-d.ReductionRatio); break;
                    case AcceleratedDriveDefinition d: ExtraTimeDrainRatio=d.ExtraDrainRatio; AcceleratedAttackSpeedBonus=d.AttackSpeedBonus; break;
                    case SacrificeDefinition d: SacrificeCostSeconds=d.CostSeconds; SacrificeAttackPerPayment=d.AttackBonusPerPayment; break;
                    case TimeVaultDefinition d: TimeVaultSeconds=d.GraceSeconds; break;
                    case OverheatCompressionDefinition d: OverheatOverflowPerStack=d.OverflowPerStack; OverheatCostSeconds=d.CostSeconds; OverheatDamageWeight=d.DamageWeight; break;
                    case OverloadMergeDefinition d: HasOverloadMerge=true; OverloadMergeCostSeconds=d.CostSeconds; OverloadMergeUpgradeChance=d.UpgradeChance; break;
                    case RecoveryHackingDefinition d: RecoveryHackingStacks=d.StacksPerRecovery; RecoveryHackingData=d.HackingData; break;
                    case GlitchBombDefinition d: GlitchBombPrefab=d.Prefab; break;
                    case GlitchAttackDefinition d: GlitchAttackPerTrigger=d.BonusPerGlitch; GlitchAttackMaximumTriggers=d.MaximumTriggers; break;
                    case BombHackingDefinition d: HasBombHacking=true; HackingStacksPerBomb=d.StacksPerBomb; break;
                    case HackingCarryoverDefinition d: HackingCarryoverRatio=d.Ratio; break;
                    case BonusManaDefinition d: BonusManaChance=d.Chance; BonusManaCount=d.ManaCount; break;
                    case EveryHitsAttackDefinition d: _hitPeriods.Add(d.HitCount); break;
                    case RandomExtraAttackDefinition d: RandomExtraAttackChance+=d.Chance; break;
                    case RandomProcAttackDefinition d: HasRandomProcAttack=true; RandomProcChance+=d.Chance; RandomProcDamagePct=d.DamageRatio; break;
                    case ExtraAttackEveryAttackDefinition d: HasExtraAttackEveryAttack=true; break;
                    case ExtraAttackOnSkillDefinition d: HasExtraAttackOnSkillFull=true; break;
                    case SkillBurstDefinition d: HasBurstOnSkillFull=true; BurstAttackBonus=Mathf.Max(BurstAttackBonus,d.AttackBonus); BurstDurationSeconds=Mathf.Max(BurstDurationSeconds,d.DurationSeconds); break;
                    case SummonDiscountDefinition d: SummonDiscountRate+=d.Ratio; break;
                    case SummonFixedDiscountDefinition d: SummonFixedDiscountAmount+=d.Amount; break;
                    case SellFoodBonusDefinition d: SellBonusFoodAmount+=d.Amount; break;
                    case SummonDamageDefinition d: HasSummonDealsDamage=true; SummonDamagePct+=d.DamageRatio; break;
                    case SellDamageDefinition d: HasSellDealsDamage=true; SellDamagePct+=d.DamageRatio; break;
                    case SellRandomUnitDefinition d: HasSellGivesRandomUnit=true; SellGivesUnitChance+=d.Chance; break;
                    case TotemOverlapDefinition d: HasAllowTotemOverlap=true; break;
                    case MergeKeepsTribeDefinition d: HasMergeKeepsTribe=true; break;
                    case ProjectileAttackScaleDefinition d: HasProjectileSizeScalesAtk=true; ProjectileSizeAtkPerUnit=d.AttackPerSizeStep; break;
                    default: throw new ArgumentException("Unsupported effect projection.");
                }
            }
        }
        Finite(AttackBonus);
        Finite(HackingOverflowAttackCoefficient);
        Finite(HackingConsumptionMultiplier);
        Finite(HackingStackDamageMultiplier);
        Finite(AttackSpeedBonus);
        Finite(TotemEfficiencyBonus);
        Finite(CritChance);
        Finite(CritDamageMultiplier);
        Finite(FoodSpeedBonus);
        Finite(ProjectileSizeBonus);
        Finite(GaugeSpeedBonus);
        Finite(ExpGainMultiplier);
        Finite(FrontAttackBonus);
        Finite(BackAttackBonus);
        Finite(FrontSpeedMultiplier);
        Finite(BackSpeedMultiplier);
        Finite(RandomExtraAttackChance);
        Finite(RandomProcChance);
        Finite(RandomProcDamagePct);
        Finite(BurstAttackBonus);
        Finite(BurstDurationSeconds);
        Finite(SummonDiscountRate);
        Finite(SummonFixedDiscountAmount);
        Finite(SellBonusFoodAmount);
        Finite(SummonDamagePct);
        Finite(SellDamagePct);
        Finite(SellGivesUnitChance);
        Finite(ProjectileSizeAtkPerUnit);
        Finite(PeriodicBombInterval);
        Finite(FleetBombInterval);
        Finite(DroneAttackSpeedBonus);
        Finite(StackDamageBonusPerDrone);
        Finite(MaximumStackDamageBonus);
        Finite(FoodPayoutInterval);
        Finite(FoodProductionBonus);
        Finite(WaveFoodDividendRatio);
        Finite(HackingProductionBonus);
        Finite(ManaRequirementReduction);
        Finite(HackingProductionProcChance);
        Finite(GammanResidueRatio);
        Finite(GammanResidueDuration);
        Finite(ChiefCooldownReduction);
        Finite(ChiefHackingDamagePerStack);
        Finite(ChiefVolleyDelay);
        Finite(ChiefVolleyDamageRatio);
        Finite(RallyDamageBonus);
        Finite(ExplosionSkillRecoverySeconds);
        Finite(MergeSupportChance);
        Finite(HackingCarryoverRatio);
        Finite(BonusManaChance);
        CritChance=Mathf.Clamp01(CritChance);
    }
    /// <summary>전방/후방 공격 가산을 기존 행 규칙으로 평가한다.</summary>
    public float GetRowAttackMultiplier(int row, int rows)
    {
        if (row<0 || row>=rows) return 1f;
        return 1f + (row<2 ? FrontAttackBonus:0f) + (rows>=2 && row==rows-1 || rows>=3 && row==rows-2 ? BackAttackBonus:0f);
    }
    /// <summary>전방/후방 승산 보정을 기존 행 규칙으로 평가한다.</summary>
    public float GetRowSpeedMultiplier(int row, int rows)
    {
        if (row<0 || row>=rows) return 1f;
        return (row<2 ? FrontSpeedMultiplier:1f) * (rows>=2 && row==rows-1 || rows>=3 && row==rows-2 ? BackSpeedMultiplier:1f);
    }
    private void ApplyStat(StatModifierDefinition d)
    {
        switch(d.Stat)
        {
            case SelectionStat.Attack: AttackBonus+=d.Ratio; break;
            case SelectionStat.AttackSpeed: AttackSpeedBonus+=d.Ratio; break;
            case SelectionStat.TotemEfficiency: TotemEfficiencyBonus+=d.Ratio; break;
            case SelectionStat.CritChance: CritChance+=d.Ratio; break;
            case SelectionStat.CritDamage: CritDamageMultiplier+=d.Ratio; break;
            case SelectionStat.FoodSpeed: FoodSpeedBonus+=d.Ratio; break;
            case SelectionStat.ProjectileSize: ProjectileSizeBonus+=d.Ratio; break;
            case SelectionStat.GaugeSpeed: GaugeSpeedBonus+=d.Ratio; break;
            case SelectionStat.Experience: ExpGainMultiplier*=1f+d.Ratio; break;
            case SelectionStat.FrontRowAttack: FrontAttackBonus+=d.Ratio; break;
            case SelectionStat.BackRowAttack: BackAttackBonus+=d.Ratio; break;
            case SelectionStat.FrontRowSpeed: FrontSpeedMultiplier*=1f+d.Ratio; break;
            case SelectionStat.BackRowSpeed: BackSpeedMultiplier*=1f+d.Ratio; break;
            default: throw new ArgumentException("Unknown stat projection.");
        }
    }
    /// <summary>Rejects within-card duplicate non-additive features using the runtime projection policy.</summary>
    public static void ValidateCardConfigurations(IEnumerable<SelectionEffectDefinition> effects)
    {
        var seen = new HashSet<Type>();
        foreach (var effect in effects)
            if (RequiresSingleConfiguration(effect) && !seen.Add(effect.GetType()))
                throw new ArgumentException("Conflicting non-additive feature configuration: " + effect.GetType().Name);
    }
    private static bool RequiresSingleConfiguration(SelectionEffectDefinition effect) => effect is
        BasicBombCountDefinition or
        BombRearmDefinition or
        BombStackFuseDefinition or
        GammanBombDefinition or
        BetanCycleDefinition or
        CounterBombDefinition or
        BombWarheadDefinition or
        BombArmorDefinition or
        BombExecutionDefinition or
        BombSalvageDefinition or
        PeriodicBombDefinition or
        FleetBombDefinition or
        StackSkillFrequencyDefinition or
        FoodPayoutDefinition or
        HackingCompressionDefinition or
        ManaRequirementDefinition or
        HackingProductionProcDefinition or
        GammanResidueDefinition or
        ChiefChargeCapacityDefinition or
        ChiefHackingDetonationDefinition or
        ChiefVolleyDefinition or
        WaveFoodDividendDefinition or
        OverflowManaDefinition or
        AcceleratedDriveDefinition or
        SacrificeDefinition or
        TimeVaultDefinition or
        OverheatCompressionDefinition or
        OverloadMergeDefinition or
        RecoveryHackingDefinition or
        GlitchBombDefinition or
        GlitchAttackDefinition or
        BombHackingDefinition or
        HackingCarryoverDefinition or
        BonusManaDefinition;
    private void Once(SelectionEffectDefinition definition) { if (!_singleConfigurations.Add(definition.GetType())) throw new ArgumentException("Conflicting non-additive feature configuration: " + definition.GetType().Name); }
    private static void Finite(float value) { if(float.IsNaN(value)||float.IsInfinity(value)) throw new ArgumentException("Selection aggregation is not finite."); }
}
