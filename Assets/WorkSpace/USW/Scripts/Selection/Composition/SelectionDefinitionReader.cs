using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>명시적 schema를 읽어 검증된 저작 DTO 사본을 만든다. 게임 상태·SO·난수를 변경하지 않는다.</summary>
public static class SelectionDefinitionReader
{
    /// <summary>지원 형식의 독립 사본을 반환한다. 이 DTO는 mutable이며 런타임 snapshot이 아니다.</summary>
    public static SelectionCardDefinition CopyDefinition(LevelUpData card)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));
        var definition = card.EffectSchemaVersion switch
        {
#if UNITY_EDITOR
            // Historical editor fixtures are transient. Authored cards must be explicitly migrated.
            0 when !UnityEditor.EditorUtility.IsPersistent(card) => CopyLegacyForMigration(card),
#endif
            1 => CopyAndValidate(card.Composition),
            _ => throw new ArgumentException($"Unsupported selection schema {card.EffectSchemaVersion}, card {card.chooseId}.")
        };
        SelectionDescriptionFormatter.ValidateTemplate(card.description, definition);
        return definition;
    }

    /// <summary>모든 참조형 설정을 복사하여 원본과 공유하지 않는다.</summary>
    public static SelectionCardDefinition CopyAndValidate(SelectionCardDefinition definition)
    {
        if (definition == null || definition.Effects == null || definition.Commands == null || definition.ExclusiveGroups == null || definition.Feedback == null)
            throw new ArgumentException("Selection definition and lists must not be null.");
        var copy = new SelectionCardDefinition();
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in definition.ExclusiveGroups)
        {
            if (string.IsNullOrWhiteSpace(group) || !groups.Add(group)) throw new ArgumentException("Empty or duplicate exclusive group.");
            copy.ExclusiveGroups.Add(group);
        }
        foreach (var target in definition.Feedback)
        {
            if (target == null || target.Tribes == null || !Enum.IsDefined(typeof(SelectionFeedbackTarget), target.Target)
                || !Enum.IsDefined(typeof(SelectionFeedbackRow), target.Row)) throw new ArgumentException("Invalid feedback target.");
            foreach (var tribe in target.Tribes)
                if (!Enum.IsDefined(typeof(UnitTribe), tribe)) throw new ArgumentException("Invalid feedback tribe.");
            copy.Feedback.Add(new SelectionFeedbackDefinition { Target=target.Target, Row=target.Row,
                RequiresHighOwnedTier=target.RequiresHighOwnedTier, Tribes=(UnitTribe[])target.Tribes.Clone() });
        }
        foreach (var effect in definition.Effects) copy.Effects.Add(CopyEffect(effect));
        foreach (var command in definition.Commands) copy.Commands.Add(CopyCommand(command));
        return copy;
    }

#if UNITY_EDITOR
    /// <summary>Editor migration only. Never used to interpret authored runtime cards.</summary>
    public static SelectionCardDefinition CopyLegacyForMigration(LevelUpData card)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));
        return CopyAndValidate(ConvertLegacy(card));
    }

    private static SelectionCardDefinition ConvertLegacy(LevelUpData card)
    {
        var definition = new SelectionCardDefinition();
        AddStat(definition, card.primaryEffect, card.primaryValue);
        AddStat(definition, card.secondaryEffect, card.secondaryValue);
        var effect = card.droneEffect;
        if (effect != null && effect.Kind != DroneSelectionKind.None)
        {
            definition.Effects.Add(ConvertDrone(effect));
            definition.ExclusiveGroups.Add("drone.kind." + (int)effect.Kind);
        }
        switch (card.specialEffect)
        {
            case LevelUpSpecialEffect.None: break;
            case LevelUpSpecialEffect.AttackEveryNHits: definition.Effects.Add(new EveryHitsAttackDefinition { HitCount = RequireWholePositive(card.specialValue) }); break;
            case LevelUpSpecialEffect.RandomBonusAttack: definition.Effects.Add(new RandomExtraAttackDefinition { Chance = card.specialValue / 100f }); break;
            case LevelUpSpecialEffect.RandomProcAttack: definition.Effects.Add(new RandomProcAttackDefinition { Chance = card.specialValue / 100f, DamageRatio = card.primaryValue / 100f }); break;
            case LevelUpSpecialEffect.ExtraAttackEveryAttack: definition.Effects.Add(new ExtraAttackEveryAttackDefinition {  }); break;
            case LevelUpSpecialEffect.ExtraAttackOnSkillFull: definition.Effects.Add(new ExtraAttackOnSkillDefinition {  }); break;
            case LevelUpSpecialEffect.BurstOnSkillFull: definition.Effects.Add(new SkillBurstDefinition { AttackBonus = card.primaryValue / 100f, DurationSeconds = card.specialValue }); break;
            case LevelUpSpecialEffect.SummonDiscount: definition.Effects.Add(new SummonDiscountDefinition { Ratio = card.specialValue / 100f }); break;
            case LevelUpSpecialEffect.SummonFixedDiscount: definition.Effects.Add(new SummonFixedDiscountDefinition { Amount = card.specialValue }); break;
            case LevelUpSpecialEffect.SellBonusFood: definition.Effects.Add(new SellFoodBonusDefinition { Amount = card.specialValue }); break;
            case LevelUpSpecialEffect.SummonDealsDamage: definition.Effects.Add(new SummonDamageDefinition { DamageRatio = card.specialValue / 100f }); break;
            case LevelUpSpecialEffect.SellDealsDamage: definition.Effects.Add(new SellDamageDefinition { DamageRatio = card.specialValue / 100f }); break;
            case LevelUpSpecialEffect.SellGivesRandomUnit: definition.Effects.Add(new SellRandomUnitDefinition { Chance = card.specialValue / 100f }); break;
            case LevelUpSpecialEffect.AllowTotemOverlap: definition.Effects.Add(new TotemOverlapDefinition {  }); break;
            case LevelUpSpecialEffect.MergeKeepsTribe: definition.Effects.Add(new MergeKeepsTribeDefinition {  }); break;
            case LevelUpSpecialEffect.ProjectileSizeScalesAtk: definition.Effects.Add(new ProjectileAttackScaleDefinition { AttackPerSizeStep = card.primaryValue / 100f }); break;
            case LevelUpSpecialEffect.GiveFoodAmount: definition.Commands.Add(new GiveFoodCommandDefinition { Amount = card.specialValue }); break;
            case LevelUpSpecialEffect.TriggerTotemSelection: definition.Commands.Add(new RequestTotemCommandDefinition()); break;
            case LevelUpSpecialEffect.GainRandomUnit: definition.Commands.Add(new GainRandomUnitCommandDefinition()); break;
            case LevelUpSpecialEffect.GuaranteeNextLegend: definition.Commands.Add(new GuaranteeLegendCommandDefinition()); break;
            case LevelUpSpecialEffect.RerollChoices: definition.Commands.Add(new RerollChoicesCommandDefinition()); break;
            case LevelUpSpecialEffect.UpgradeDiscountAndRefund:
                definition.Effects.Add(new UpgradeDiscountDefinition { Ratio = card.specialValue / 100f });
                definition.Commands.Add(new RefundUpgradeCommandDefinition { Ratio = card.specialValue / 100f });
                break;
            default: throw new ArgumentException($"Legacy special effect {card.specialEffect} is not migrated yet, card {card.chooseId}.");
        }
        AddLegacyFeedback(card, definition);
        return definition;
    }

    private static void AddLegacyFeedback(LevelUpData card, SelectionCardDefinition definition)
    {
        var kind = card.droneEffect?.Kind ?? DroneSelectionKind.None;
        if (kind != DroneSelectionKind.None)
        {
            SelectionFeedbackTarget? target = kind switch
            {
                DroneSelectionKind.BetanPeriodicBomb or DroneSelectionKind.BetanAttackSpeed or DroneSelectionKind.BetanFleetBomb or DroneSelectionKind.BetanRepairKit => SelectionFeedbackTarget.Betan,
                DroneSelectionKind.GammanFrequency or DroneSelectionKind.GammanEmergency => SelectionFeedbackTarget.Gamman,
                DroneSelectionKind.ZeltanAirFryer or DroneSelectionKind.ZeltanColdStorage or DroneSelectionKind.ZeltanMaintenance => SelectionFeedbackTarget.Zeltan,
                DroneSelectionKind.DeltanDefenseReduction or DroneSelectionKind.DeltanDamageTaken or DroneSelectionKind.DeltanCooldown => SelectionFeedbackTarget.Deltan,
                DroneSelectionKind.AlphanCooldown or DroneSelectionKind.AlphanDoubleShot or DroneSelectionKind.AlphanGoldenMonocle => SelectionFeedbackTarget.ChiefSkill,
                DroneSelectionKind.ExtraCombatDrone => SelectionFeedbackTarget.CombatDroneOwner,
                _ => null
            };
            if (target.HasValue) definition.Feedback.Add(new SelectionFeedbackDefinition { Target=target.Value,
                RequiresHighOwnedTier=kind == DroneSelectionKind.ExtraCombatDrone });
            return;
        }
        bool IsUnitStat(LevelUpEffectType type) => type != LevelUpEffectType.None
            && type != LevelUpEffectType.TotemEfficiencyPercent && type != LevelUpEffectType.ExpGainPercent;
        if (!IsUnitStat(card.primaryEffect) && !IsUnitStat(card.secondaryEffect)) return;
        var row = card.primaryEffect switch
        {
            LevelUpEffectType.FrontRowAttackPercent or LevelUpEffectType.FrontRowSpeedPercent => SelectionFeedbackRow.Front,
            LevelUpEffectType.BackRowAttackPercent or LevelUpEffectType.BackRowSpeedPercent => SelectionFeedbackRow.Back,
            _ => SelectionFeedbackRow.All
        };
        definition.Feedback.Add(new SelectionFeedbackDefinition { Target=SelectionFeedbackTarget.AllUnits,
            Row=row, Tribes=card.applicableTribes == null ? Array.Empty<UnitTribe>() : (UnitTribe[])card.applicableTribes.Clone() });
    }

    private static void AddStat(SelectionCardDefinition definition, LevelUpEffectType kind, float percent)
    {
        if (kind == LevelUpEffectType.None) return;
        int value = (int)kind;
        if (value < 1 || value > 13) throw new ArgumentException($"Unsupported legacy stat {kind}.");
        definition.Effects.Add(new StatModifierDefinition { Stat = (SelectionStat)value, Ratio = percent / 100f });
    }

    private static SelectionEffectDefinition ConvertDrone(DroneSelectionEffect effect)
    {
        switch ((int)effect.Kind)
        {
            case 1: return new PeriodicBombDefinition { PeriodSeconds=effect.Interval, BombCount=effect.Count };
            case 2: return new DroneAttackSpeedDefinition { BonusRatio=effect.Value };
            case 3: return new FleetBombDefinition { PeriodSeconds=effect.Interval, BombCount=effect.Count };
            case 4: return new StackSkillFrequencyDefinition { ReductionPerStack=effect.Value, MaximumReduction=effect.MaxValue };
            case 5: return new EmptyStackDamageDefinition {  };
            case 6: return new FoodPayoutDefinition { PeriodSeconds=effect.Interval, BonusRatio=effect.Value };
            case 7: return new HackingProductionFlatDefinition { StackCount=effect.Count };
            case 8: return new HackingProductionRollDefinition { MinimumRatio=effect.Value, MaximumRatio=effect.MaxValue };
            case 9: return new ManaRequirementDefinition { ReductionRatio=effect.Value };
            case 10: return new ChiefCooldownDefinition { ReductionRatio=effect.Value };
            case 11: return new ChiefVolleyDefinition { ShotDelaySeconds=effect.Interval, DamageRatio=effect.Value, ShotCount=effect.Count };
            case 12: return new FoodProductionDefinition { BonusRatio=effect.Value };
            case 13: return new FleetFoodProductionDefinition { BonusRatio=effect.Value };
            case 14: return new RallyDamageDefinition { BonusRatio=effect.Value };
            case 15: return new ExplosionSkillRecoveryDefinition { Seconds=effect.Value };
            case 16: return new MergeSupportDefinition { Chance=effect.Value };
            case 17: return new CombatDroneCapacityDefinition { Count=effect.Count };
            case 18: return new OverflowManaDefinition { ManaPerStack=effect.Count };
            case 19: return new BombHackingDefinition { StacksPerBomb=effect.Count };
            case 20: return new HackingCarryoverDefinition { Ratio=effect.Value };
            case 21: return new BonusManaDefinition { Chance=effect.Value, ManaCount=effect.Count };
            default: throw new ArgumentException($"Unsupported legacy drone kind {effect.Kind}.");
        }
    }

#endif

    private static SelectionEffectDefinition CopyEffect(SelectionEffectDefinition effect)
    {
        switch (effect)
        {
            case EveryHitsAttackDefinition d:
                Positive(d.HitCount, nameof(d.HitCount));
                return new EveryHitsAttackDefinition { HitCount = d.HitCount };
            case RandomExtraAttackDefinition d:
                Ratio(d.Chance, nameof(d.Chance));
                return new RandomExtraAttackDefinition { Chance = d.Chance };
            case RandomProcAttackDefinition d:
                Ratio(d.Chance, nameof(d.Chance)); NonNegative(d.DamageRatio, nameof(d.DamageRatio));
                return new RandomProcAttackDefinition { Chance = d.Chance, DamageRatio = d.DamageRatio };
            case ExtraAttackEveryAttackDefinition d:

                return new ExtraAttackEveryAttackDefinition {  };
            case ExtraAttackOnSkillDefinition d:

                return new ExtraAttackOnSkillDefinition {  };
            case SkillBurstDefinition d:
                NonNegative(d.AttackBonus, nameof(d.AttackBonus)); NonNegative(d.DurationSeconds, nameof(d.DurationSeconds));
                return new SkillBurstDefinition { AttackBonus = d.AttackBonus, DurationSeconds = d.DurationSeconds };
            case SummonDiscountDefinition d:
                Ratio(d.Ratio, nameof(d.Ratio));
                return new SummonDiscountDefinition { Ratio = d.Ratio };
            case SummonFixedDiscountDefinition d:
                NonNegative(d.Amount, nameof(d.Amount));
                return new SummonFixedDiscountDefinition { Amount = d.Amount };
            case SellFoodBonusDefinition d:
                NonNegative(d.Amount, nameof(d.Amount));
                return new SellFoodBonusDefinition { Amount = d.Amount };
            case SummonDamageDefinition d:
                NonNegative(d.DamageRatio, nameof(d.DamageRatio));
                return new SummonDamageDefinition { DamageRatio = d.DamageRatio };
            case SellDamageDefinition d:
                NonNegative(d.DamageRatio, nameof(d.DamageRatio));
                return new SellDamageDefinition { DamageRatio = d.DamageRatio };
            case SellRandomUnitDefinition d:
                Ratio(d.Chance, nameof(d.Chance));
                return new SellRandomUnitDefinition { Chance = d.Chance };
            case TotemOverlapDefinition d:

                return new TotemOverlapDefinition {  };
            case MergeKeepsTribeDefinition d:

                return new MergeKeepsTribeDefinition {  };
            case ProjectileAttackScaleDefinition d:
                NonNegative(d.AttackPerSizeStep, nameof(d.AttackPerSizeStep));
                return new ProjectileAttackScaleDefinition { AttackPerSizeStep = d.AttackPerSizeStep };
            case StatModifierDefinition d:
                if (!Enum.IsDefined(typeof(SelectionStat), d.Stat)) throw new ArgumentException("Unknown selection stat.");
                Finite(d.Ratio, nameof(d.Ratio));
                return new StatModifierDefinition { Stat = d.Stat, Ratio = d.Ratio };
            case UpgradeDiscountDefinition d:
                Ratio(d.Ratio, nameof(d.Ratio));
                return new UpgradeDiscountDefinition { Ratio = d.Ratio };
            case PeriodicBombDefinition d:
                Positive(d.PeriodSeconds, nameof(d.PeriodSeconds)); Positive(d.BombCount, nameof(d.BombCount));
                return new PeriodicBombDefinition { PeriodSeconds = d.PeriodSeconds, BombCount = d.BombCount };
            case DroneAttackSpeedDefinition d:
                NonNegative(d.BonusRatio, nameof(d.BonusRatio));
                return new DroneAttackSpeedDefinition { BonusRatio = d.BonusRatio };
            case FleetBombDefinition d:
                Positive(d.PeriodSeconds, nameof(d.PeriodSeconds)); Positive(d.BombCount, nameof(d.BombCount));
                return new FleetBombDefinition { PeriodSeconds = d.PeriodSeconds, BombCount = d.BombCount };
            case StackSkillFrequencyDefinition d:
                Ratio(d.ReductionPerStack, nameof(d.ReductionPerStack)); Ratio(d.MaximumReduction, nameof(d.MaximumReduction));
                return new StackSkillFrequencyDefinition { ReductionPerStack = d.ReductionPerStack, MaximumReduction = d.MaximumReduction };
            case EmptyStackDamageDefinition d:

                return new EmptyStackDamageDefinition {  };
            case FoodPayoutDefinition d:
                Positive(d.PeriodSeconds, nameof(d.PeriodSeconds)); NonNegative(d.BonusRatio, nameof(d.BonusRatio));
                return new FoodPayoutDefinition { PeriodSeconds = d.PeriodSeconds, BonusRatio = d.BonusRatio };
            case HackingProductionFlatDefinition d:
                Positive(d.StackCount, nameof(d.StackCount));
                return new HackingProductionFlatDefinition { StackCount = d.StackCount };
            case HackingProductionRollDefinition d:
                PercentStep(d.MinimumRatio, nameof(d.MinimumRatio)); PercentStep(d.MaximumRatio, nameof(d.MaximumRatio)); if (d.MinimumRatio > d.MaximumRatio) throw new ArgumentException("MinimumRatio exceeds MaximumRatio.");
                return new HackingProductionRollDefinition { MinimumRatio = d.MinimumRatio, MaximumRatio = d.MaximumRatio };
            case ManaRequirementDefinition d:
                Ratio(d.ReductionRatio, nameof(d.ReductionRatio));
                return new ManaRequirementDefinition { ReductionRatio = d.ReductionRatio };
            case ChiefCooldownDefinition d:
                Ratio(d.ReductionRatio, nameof(d.ReductionRatio));
                return new ChiefCooldownDefinition { ReductionRatio = d.ReductionRatio };
            case ChiefVolleyDefinition d:
                NonNegative(d.ShotDelaySeconds, nameof(d.ShotDelaySeconds)); NonNegative(d.DamageRatio, nameof(d.DamageRatio)); Positive(d.ShotCount, nameof(d.ShotCount));
                return new ChiefVolleyDefinition { ShotDelaySeconds = d.ShotDelaySeconds, DamageRatio = d.DamageRatio, ShotCount = d.ShotCount };
            case FoodProductionDefinition d:
                NonNegative(d.BonusRatio, nameof(d.BonusRatio));
                return new FoodProductionDefinition { BonusRatio = d.BonusRatio };
            case FleetFoodProductionDefinition d:
                NonNegative(d.BonusRatio, nameof(d.BonusRatio));
                return new FleetFoodProductionDefinition { BonusRatio = d.BonusRatio };
            case RallyDamageDefinition d:
                NonNegative(d.BonusRatio, nameof(d.BonusRatio));
                return new RallyDamageDefinition { BonusRatio = d.BonusRatio };
            case ExplosionSkillRecoveryDefinition d:
                NonNegative(d.Seconds, nameof(d.Seconds));
                return new ExplosionSkillRecoveryDefinition { Seconds = d.Seconds };
            case MergeSupportDefinition d:
                Ratio(d.Chance, nameof(d.Chance));
                return new MergeSupportDefinition { Chance = d.Chance };
            case CombatDroneCapacityDefinition d:
                Positive(d.Count, nameof(d.Count));
                return new CombatDroneCapacityDefinition { Count = d.Count };
            case OverflowManaDefinition d:
                Positive(d.ManaPerStack, nameof(d.ManaPerStack));
                return new OverflowManaDefinition { ManaPerStack = d.ManaPerStack };
            case BombHackingDefinition d:
                Positive(d.StacksPerBomb, nameof(d.StacksPerBomb));
                return new BombHackingDefinition { StacksPerBomb = d.StacksPerBomb };
            case HackingCarryoverDefinition d:
                Ratio(d.Ratio, nameof(d.Ratio));
                return new HackingCarryoverDefinition { Ratio = d.Ratio };
            case BonusManaDefinition d:
                Ratio(d.Chance, nameof(d.Chance)); Positive(d.ManaCount, nameof(d.ManaCount));
                return new BonusManaDefinition { Chance = d.Chance, ManaCount = d.ManaCount };
            default: throw new ArgumentException("Null or unsupported selection effect definition.");
        }
    }

    private static SelectionCommandDefinition CopyCommand(SelectionCommandDefinition command)
    {
        switch (command)
        {
            case GuaranteeLegendCommandDefinition _: return new GuaranteeLegendCommandDefinition();
            case RerollChoicesCommandDefinition _: return new RerollChoicesCommandDefinition();
            case RequestTotemCommandDefinition _: return new RequestTotemCommandDefinition();
            case GainRandomUnitCommandDefinition _: return new GainRandomUnitCommandDefinition();
            case RefundUpgradeCommandDefinition d: Ratio(d.Ratio, nameof(d.Ratio)); return new RefundUpgradeCommandDefinition { Ratio = d.Ratio };
            case GiveFoodCommandDefinition d: NonNegative(d.Amount, nameof(d.Amount)); return new GiveFoodCommandDefinition { Amount = d.Amount };
            default: throw new ArgumentException("Null or unsupported selection command definition.");
        }
    }

    private static int RequireWholePositive(float value) { Positive(value, nameof(value)); if (value >= int.MaxValue || value != Mathf.Floor(value)) throw new ArgumentException("Hit count must be a positive integer."); return (int)value; }
    private static void Finite(float value, string field) { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException(field + " must be finite."); }
    private static void NonNegative(float value, string field) { Finite(value, field); if (value < 0) throw new ArgumentException(field + " must be nonnegative."); }
    private static void Positive(float value, string field) { Finite(value, field); if (value <= 0) throw new ArgumentException(field + " must be positive."); }
    private static void Ratio(float value, string field) { NonNegative(value, field); if (value > 1) throw new ArgumentException(field + " must be at most one."); }
    private static void PercentStep(float value, string field) { Ratio(value, field); if (Mathf.Abs(value * 100f - Mathf.Round(value * 100f)) > 0.0001f) throw new ArgumentException(field + " must use whole percent steps."); }
}
