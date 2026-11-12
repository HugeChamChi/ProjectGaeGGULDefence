using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GGD.BackendCharts;
using UnityEngine;

// Current totem numbers: the TotemBalance chart value when the chart has a key for this totem, otherwise the value authored in this asset.
// Combat and the description text read the same functions, so they cannot drift apart (TODO G12, user answer Q3 2026-11-11).
public partial class TotemData
{
    private float[] _stageSecondsSource, _stageBonusSource;
    private List<TotemGrowthStage> _currentStages;

    /// <summary>Current value of a named number (names are the constants on <see cref="TotemBalance"/>).</summary>
    public float Number(string name) => TotemBalance.Number(totemId, name, BundledNumber(name));

    /// <summary>Value authored in this asset for a named number. NaN when this asset has no such number.</summary>
    public float BundledNumber(string name)
    {
        switch (name)
        {
            case TotemBalance.Enabled: return 1f;
            case TotemBalance.Chance: return BonusProjectile.Chance;
            case TotemBalance.AttackCoefficient: return BonusProjectile.AttackCoefficient;
            case TotemBalance.HarvestSeconds: return GrowthHarvestSeconds;
            case TotemBalance.HarvestFood: return GrowthHarvestFood;
            case TotemBalance.FireIntervalSeconds: return (float)_debuffFireInterval;
            case TotemBalance.ImpactDamage: return (float)_debuffImpactDamage;
            case TotemBalance.SpawnIntervalSeconds: return WildcardSpawn.IntervalSeconds;
            case TotemBalance.ShadowDelaySeconds: return ShadowAttack.DelaySeconds;
            case TotemBalance.FoodIntervalSeconds: return GetFoodGenerator()?.interval ?? float.NaN;
            case TotemBalance.Food: return GetFoodGenerator()?.amount ?? float.NaN;
            case TotemBalance.AttackBonus: return FindSimple(StatKind.AttackPercent)?.amount ?? float.NaN;
            case TotemBalance.SpeedBonus: return FindSimple(StatKind.Speed)?.amount ?? float.NaN;
            case TotemBalance.CritChanceBonus: return FindSimple(StatKind.CritChance)?.amount ?? float.NaN;
            case TotemBalance.CritDamageBonus: return FindSimple(StatKind.CritDamage)?.amount ?? float.NaN;
            case TotemBalance.WindowSeconds: return FindEncounterWindow()?.seconds ?? float.NaN;
            default: return float.NaN;
        }
    }

    /// <summary>Values authored in this asset for a named list (TD1002 growth stages). Null when the name is not a list.</summary>
    public float[] BundledList(string name)
    {
        if (name != TotemBalance.StageSeconds && name != TotemBalance.StageAttackBonus) return null;
        var values = new float[GrowthStages.Count];
        for (int i = 0; i < values.Length; i++)
            values[i] = name == TotemBalance.StageSeconds ? GrowthStages[i].RequiredSeconds : GrowthStages[i].AttackBonus;
        return values;
    }

    /// <summary>Current amount of a simple buff of this totem. <paramref name="bundled"/> is the amount authored on the function.</summary>
    public float SimpleAmount(StatKind kind, float bundled)
    {
        string name = SimpleName(kind);
        return name == null ? bundled : TotemBalance.Number(totemId, name, bundled);
    }

    /// <summary>Current boss-encounter window of this totem. <paramref name="bundled"/> is the value authored on the condition.</summary>
    public float EncounterWindowSeconds(float bundled) => TotemBalance.Number(totemId, TotemBalance.WindowSeconds, bundled);

    public float BonusProjectileChance => Number(TotemBalance.Chance);
    public float BonusProjectileAttackCoefficient => Number(TotemBalance.AttackCoefficient);
    public float CurrentGrowthHarvestSeconds => Number(TotemBalance.HarvestSeconds);
    public float CurrentGrowthHarvestFood => Number(TotemBalance.HarvestFood);
    public float WildcardIntervalSeconds => Number(TotemBalance.SpawnIntervalSeconds);
    public float ShadowDelaySeconds => Number(TotemBalance.ShadowDelaySeconds);
    public float FoodIntervalSeconds => Number(TotemBalance.FoodIntervalSeconds);
    public float FoodAmount => Number(TotemBalance.Food);

    /// <summary>
    /// Growth stages with the current times and attack bonuses. Names and cell ranges always come from the asset.
    /// Returns the authored list itself when no chart value applies.
    /// </summary>
    public List<TotemGrowthStage> CurrentGrowthStages
    {
        get
        {
            float[] seconds = TotemBalance.List(totemId, TotemBalance.StageSeconds);
            float[] bonus = TotemBalance.List(totemId, TotemBalance.StageAttackBonus);
            if (seconds == null || bonus == null || GrowthStages == null) return GrowthStages;
            if (seconds.Length != GrowthStages.Count || bonus.Length != GrowthStages.Count)
            {
                Debug.LogError($"[TotemData] {name}: chart has {seconds.Length} growth stages, the asset has {GrowthStages.Count}. Using the asset values.");
                return GrowthStages;
            }
            if (_currentStages == null || !ReferenceEquals(seconds, _stageSecondsSource) || !ReferenceEquals(bonus, _stageBonusSource))
            {
                _currentStages = new List<TotemGrowthStage>(GrowthStages.Count);
                for (int i = 0; i < GrowthStages.Count; i++)
                    _currentStages.Add(new TotemGrowthStage
                    {
                        Name = GrowthStages[i].Name, RequiredSeconds = seconds[i], AttackBonus = bonus[i], Offsets = GrowthStages[i].Offsets
                    });
                _stageSecondsSource = seconds;
                _stageBonusSource = bonus;
            }
            return _currentStages;
        }
    }

    /// <summary>
    /// Fills number tokens in a description. <c>{Name}</c> becomes the current number, <c>{Name%}</c> becomes it as a percent
    /// without sign (0.2 and -0.2 both read "20%"; the sentence says whether it rises or falls). Unknown tokens are left as they are.
    /// </summary>
    public string FillNumbers(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        var result = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            int close = text[i] == '{' ? text.IndexOf('}', i + 1) : -1;
            if (close < 0) { result.Append(text[i]); i++; continue; }
            string token = text.Substring(i + 1, close - i - 1);
            bool percent = token.EndsWith("%");
            string name = percent ? token.Substring(0, token.Length - 1) : token;
            if (float.IsNaN(BundledNumber(name))) { result.Append(text, i, close - i + 1); i = close + 1; continue; }
            float value = Number(name);
            result.Append(percent
                ? Mathf.Abs(value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%"
                : value.ToString("0.##", CultureInfo.InvariantCulture));
            i = close + 1;
        }
        return result.ToString();
    }

    private static string SimpleName(StatKind kind)
    {
        switch (kind)
        {
            case StatKind.AttackPercent: return TotemBalance.AttackBonus;
            case StatKind.Speed: return TotemBalance.SpeedBonus;
            case StatKind.CritChance: return TotemBalance.CritChanceBonus;
            case StatKind.CritDamage: return TotemBalance.CritDamageBonus;
            default: return null;
        }
    }

    // First simple buff of the kind anywhere in this asset: top-level functions, effect groups, and inside conditional buffs.
    private SimpleBuffFunction FindSimple(StatKind kind)
    {
        foreach (var function in AllFunctions())
        {
            var simple = function as SimpleBuffFunction ?? (function as ConditionalBuffFunction)?.buff;
            if (simple != null && simple.kind == kind) return simple;
        }
        return null;
    }

    private BossEncounterWindowCondition FindEncounterWindow()
    {
        foreach (var function in AllFunctions())
            if (function is ConditionalBuffFunction { condition: BossEncounterWindowCondition window }) return window;
        return null;
    }

    private IEnumerable<ITotemFunction> AllFunctions()
    {
        if (functions != null) foreach (var function in functions) yield return function;
        if (EffectGroups != null)
            foreach (var group in EffectGroups)
                if (group?.Functions != null) foreach (var function in group.Functions) yield return function;
    }
}
