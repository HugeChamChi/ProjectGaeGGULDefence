using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>확정 정의만 읽어 카드 설명을 만든다. 난수를 소비하거나 SO를 변경하지 않는다.</summary>
public sealed class SelectionDescriptionFormatter
{
    private readonly IDescriptionTermResolver _terms;

    /// <summary>공용 용어 사전으로 결과 기록의 비상호작용 문구도 표시한다.</summary>
    public SelectionDescriptionFormatter(DescriptionTermCatalog catalog = null)
    {
        if (catalog != null) _terms = new DescriptionTermResolver(catalog);
    }

    /// <summary>기존 저작 문구와 확정 랜덤/마나/해킹 토큰을 표시한다.</summary>
    public string Format(SelectionCardSnapshot card)
    {
        if (card == null) return string.Empty;
        return DescriptionFormatter.Format(Capture(card, null), true, _terms, false);
    }

    /// <summary>확정된 조합형 수치를 상세/요약 양쪽에 제공하며 난수를 다시 뽑지 않는다.</summary>
    public DescriptionSnapshot Capture(SelectionCardSnapshot card, string simple)
    {
        if (card == null) return new DescriptionSnapshot(null, simple);
        var values = Values(card.CopyDefinition());
        return new DescriptionSnapshot((card.DescriptionTemplate ?? string.Empty).Replace("[1~10%]", "{value}%"), simple, values);
    }

    /// <summary>Rejects unresolved or malformed placeholders before a card enters the catalog.</summary>
    public static void ValidateTemplate(string template, SelectionCardDefinition definition)
        => FormatTemplate(template, definition);

    private static string FormatTemplate(string template, SelectionCardDefinition definition)
    {
        string text = (template ?? string.Empty).Replace("[1~10%]", "{value}%");
        foreach (var pair in Values(definition)) text = text.Replace("{" + pair.Key + "}", pair.Value);
        // Explicit term tokens belong to the common description renderer, not the numeric validator.
        string numericText = Regex.Replace(text, @"\{term:[a-z0-9:-]+\}", string.Empty);
        if (numericText.IndexOf('{') >= 0 || numericText.IndexOf('}') >= 0)
            throw new ArgumentException("Selection description contains an unresolved or malformed token: " + text);
        return text;
    }

    private static Dictionary<string, string> Values(SelectionCardDefinition definition)
    {
        var values = new Dictionary<string, string>();
        foreach (var effect in definition.Effects)
        {
            switch (effect)
            {
                case HackingProductionRollDefinition d:
                    values["value"] = Percent(d.MinimumRatio); break;
                case HackingProductionFlatDefinition d: values["Count"] = d.StackCount.ToString(CultureInfo.InvariantCulture); break;
                case HackingCompressionDefinition d:
                    values["count"] = Number(d.RequiredAttacks); values["value"] = Number(d.ProductionMultiplier); break;
                case ManaRequirementDefinition d: values["ValuePercent"] = Percent(d.ReductionRatio); break;
                case HackingProductionProcDefinition d:
                    values["value"] = Percent(d.Chance); values["count"] = d.Multiplier.ToString(CultureInfo.InvariantCulture); break;
                case GammanResidueDefinition d:
                    values["value"] = Percent(d.DamageRatio); values["seconds"] = Number(d.DurationSeconds); values["count"] = d.TickCount.ToString(CultureInfo.InvariantCulture); break;
                case PeriodicBombDefinition d:
                    values["seconds"] = Number(d.PeriodSeconds); values["count"] = Number(d.BombCount); break;
                case DroneAttackSpeedDefinition d: values["value"] = Percent(d.BonusRatio); break;
                case FleetBombDefinition d:
                    values["seconds"] = Number(d.PeriodSeconds); values["count"] = Number(d.BombCount); break;
                case StackSkillFrequencyDefinition d:
                    values["value"] = Percent(d.ReductionPerStack); values["max"] = Percent(d.MaximumReduction); break;
                case FoodPayoutDefinition d:
                    values["value"] = Percent(d.BonusRatio); values["seconds"] = Number(d.PeriodSeconds); break;
                case ChiefCooldownDefinition d: values["value"] = Percent(d.ReductionRatio); break;
                case ChiefChargeCapacityDefinition d: values["count"] = Number(d.MaximumCharges); break;
                case ChiefHackingDetonationDefinition d: values["value"] = Percent(d.DamageRatioPerStack); break;
                case ChiefVolleyDefinition d:
                    values["value"] = Percent(d.DamageRatio); values["count"] = Number(d.ShotCount); values["seconds"] = Number(d.ShotDelaySeconds); break;
                case FoodProductionDefinition d: values["value"] = Percent(d.BonusRatio); break;
                case WaveFoodDividendDefinition d: values["value"] = Percent(d.Ratio); break;
                case RallyDamageDefinition d: values["value"] = Number(d.BonusRatio); break;
                case ExplosionSkillRecoveryDefinition d: values["seconds"] = Number(d.Seconds); break;
                case UpgradeDiscountDefinition d: values["value"] = Percent(d.Ratio); break;
                case MergeSupportDefinition d: values["value"] = Percent(d.Chance); break;
                case CombatDroneCapacityDefinition d: values["count"] = Number(d.Count); break;
                case OverflowManaDefinition d: values["count"] = d.ManaPerCast.ToString(CultureInfo.InvariantCulture); break;
                case BombHackingDefinition d: values["count"] = d.StacksPerBomb.ToString(CultureInfo.InvariantCulture); break;
                case HackingCarryoverDefinition d: values["value"] = Percent(d.Ratio); break;
                case BonusManaDefinition d:
                    values["value"] = Percent(d.Chance);
                    values["count"] = d.ManaCount.ToString(CultureInfo.InvariantCulture); break;
            }
        }
        foreach (var command in definition.Commands)
            if (command is RefundUpgradeCommandDefinition refund) values["refund"] = Percent(refund.Ratio);
        return values;
    }
    private static string Percent(float ratio) => (ratio * 100f).ToString("0.#", CultureInfo.InvariantCulture);
    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
