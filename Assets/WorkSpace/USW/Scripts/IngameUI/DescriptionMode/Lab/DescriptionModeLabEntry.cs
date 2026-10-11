using System;
using System.Globalization;
using UnityEngine;

/// <summary>Lab-only adapter for the older worktree's authored selection and totem data.</summary>
[Serializable]
public sealed class DescriptionModeLabEntry
{
    public LevelUpData Selection;
    public TotemData Totem;
    [TextArea] public string BriefTemplate;
    [TextArea] public string DetailedTemplate;

    /// <summary>Authored display title.</summary>
    public string Title => Selection != null ? Selection.chooseName : Totem != null ? Totem.totemName : string.Empty;
    /// <summary>Authored preview artwork.</summary>
    public Sprite Icon => Selection != null ? Selection.icon : Totem != null ? Totem.DisplaySprite : null;

    /// <summary>Resolves both descriptions together from the same data, without rolling any effect.</summary>
    public string[] Resolve()
    {
        string brief = BriefTemplate ?? string.Empty;
        string detail = DetailedTemplate ?? string.Empty;
        if (Selection != null && Selection.droneEffect != null)
        {
            var effect = Selection.droneEffect;
            Replace(ref brief, ref detail, "{percent}", Number(effect.Value * 100f));
            Replace(ref brief, ref detail, "{count}", effect.Count.ToString(CultureInfo.InvariantCulture));
            Replace(ref brief, ref detail, "{interval}", Number(effect.Interval));
        }
        if (Totem != null)
        {
            Replace(ref brief, ref detail, "{chance}", Number(Totem.BonusProjectile.Chance * 100f));
            Replace(ref brief, ref detail, "{coefficient}", Number(Totem.BonusProjectile.AttackCoefficient * 100f));
            Replace(ref brief, ref detail, "{attack}", Number(Totem.GetSimpleAmount(StatKind.AttackPercent) * 100f));
            Replace(ref brief, ref detail, "{speedPenalty}", Number(-Totem.GetSimpleAmount(StatKind.Speed) * 100f));
            if (Totem.GrowthStages != null && Totem.GrowthStages.Count > 0)
            {
                var first = Totem.GrowthStages[0];
                var last = Totem.GrowthStages[Totem.GrowthStages.Count - 1];
                Replace(ref brief, ref detail, "{start}", Number(first.AttackBonus * 100f));
                Replace(ref brief, ref detail, "{end}", Number(last.AttackBonus * 100f));
                Replace(ref brief, ref detail, "{mature}", Number(last.RequiredSeconds / 60f));
                Replace(ref brief, ref detail, "{harvest}", Number(Totem.GrowthHarvestSeconds / 60f));
                Replace(ref brief, ref detail, "{food}", Number(Totem.GrowthHarvestFood));
                Replace(ref brief, ref detail, "{growthDetails}", Totem.GetDisplayDescription());
            }
            Color color = Totem.HasEffectGroups ? Totem.EffectGroups[0].Color : Totem.descriptionHighlightColor;
            Replace(ref brief, ref detail, "[", "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">");
            Replace(ref brief, ref detail, "]", "</color>");
        }
        if (brief.Contains("{") || detail.Contains("{")) throw new InvalidOperationException("Unresolved lab description: " + Title);
        return new[] { brief, detail };
    }

    private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    private static void Replace(ref string brief, ref string detail, string key, string value)
    { brief = brief.Replace(key, value); detail = detail.Replace(key, value); }
}
