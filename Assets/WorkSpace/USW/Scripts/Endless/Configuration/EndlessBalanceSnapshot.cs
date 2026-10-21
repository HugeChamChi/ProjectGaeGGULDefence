using System;
using System.Collections.Generic;
using System.Globalization;
using LitJson;

/// <summary>Complete remote NORMAL balance. Missing fields never inherit local SO values.</summary>
public sealed class EndlessBalanceSnapshot
{
    /// <summary>Starting countdown in seconds.</summary>
    public float StartSeconds { get; }
    /// <summary>HP growth percentage; 13 means multiplication by 1.13.</summary>
    public double HpGrowthPercent { get; }
    /// <summary>Breather boss time reward in seconds.</summary>
    public float BreatherSeconds { get; }
    /// <summary>Hard boss time reward in seconds.</summary>
    public float HardSeconds { get; }
    /// <summary>Operator-assigned revision for diagnostics and simulator exports.</summary>
    public string BalanceVersion { get; }
    /// <summary>Remote HP, defense and line configuration frozen for this run.</summary>
    public EndlessBossProgressionSnapshot BossProgression { get; }
    /// <summary>Fingerprint of the complete accepted chart payload, including combat EXP rates.</summary>
    public string ContentHash { get; private set; }
    private IReadOnlyList<string> _breatherBossKeys;
    private IReadOnlyList<string> _hardBossKeys;

    /// <summary>Frozen, operator-authored roster for a role. Keys resolve only to installed content.</summary>
    public IReadOnlyList<string> BossKeys(EndlessBossRole role) => role == EndlessBossRole.Hard ? _hardBossKeys : _breatherBossKeys;
    private readonly Dictionary<string, double> _combatExpRates;
    private readonly int _combatExpFirstRound;
    private readonly int _combatExpInterval;
    private readonly double _combatExpGrowthPercent;

    private EndlessBalanceSnapshot(float start, double hp, float breather, float hard, string version,
        EndlessBossProgressionSnapshot progression, Dictionary<string, double> rates, int expFirst, int expInterval, double expGrowth)
    {
        StartSeconds = start; HpGrowthPercent = hp; BreatherSeconds = breather; HardSeconds = hard;
        BalanceVersion = version; BossProgression = progression; _combatExpRates = rates;
        _combatExpFirstRound = expFirst; _combatExpInterval = expInterval; _combatExpGrowthPercent = expGrowth;
    }

    /// <summary>Looks up stable pool keys; remote values preserve the existing per-damage combat EXP curve.</summary>
    public bool TryGetCombatExpPerHp(string bossKey, int round, out float rate)
    {
        rate = 0;
        if (round < 1 || bossKey == null || !_combatExpRates.TryGetValue(bossKey, out double initial)) return false;
        int steps = round < _combatExpFirstRound ? 0 : 1 + (round - _combatExpFirstRound) / _combatExpInterval;
        double value = initial * Math.Pow((1d + _combatExpGrowthPercent / 100d) / (1d + HpGrowthPercent / 100d), steps);
        rate = (float)value;
        return Finite(value) && value >= 0 && value <= float.MaxValue && (initial == 0 || rate > 0);
    }

    /// <summary>Requires one complete NORMAL row. The original six columns remain compatible with older clients.</summary>
    public static bool TryParse(JsonData rows, out EndlessBalanceSnapshot snapshot)
    {
        snapshot = null;
        try
        {
            if (rows == null || !rows.IsArray || rows.Count != 1) return false;
            var row = rows[0];
            if (row["Mode"].ToString() != "NORMAL" || row["SchemaVersion"].ToString() != "1") return false;
            string version = row["BalanceVersion"].ToString();
            if (!EndlessConfigurationValidator.IsValidKey(version) || version.Length > 100) return false;
            if (!Read(row, "StartSeconds", out double start) || start <= 0 || start > float.MaxValue
                || !Read(row, "HpGrowthPercent", out double hp)
                || !Read(row, "BreatherSeconds", out double breather) || breather > float.MaxValue
                || !Read(row, "HardSeconds", out double hard) || hard > float.MaxValue
                || !Integer(row, "BreatherBaseHp", out long baseHp)
                || !Int32(row, "FirstGrowthRound", out int first)
                || !Int32(row, "GrowthIntervalRounds", out int interval)
                || !Int32(row, "HardReferenceRound", out int hardRound)
                || !Int32(row, "BreatherReferenceRound", out int breatherRound)
                || !Read(row, "HardReferenceMultiplier", out double multiplier)
                || !Integer(row, "HpPerLine", out long hpPerLine)
                || !Read(row, "BreatherReductionPercent", out double breatherReduction)
                || !Read(row, "HardReductionPercent", out double hardReduction)
                || !Read(row, "DefenseScale", out double scale)
                || !Int32(row, "GrowthRateFinalized", out int finalized) || finalized > 1
                || !Int32(row, "CombatExpFirstGrowthRound", out int expFirst) || expFirst < 1
                || !Int32(row, "CombatExpGrowthInterval", out int expInterval) || expInterval < 1
                || !Read(row, "CombatExpGrowthPercent", out double expGrowth)) return false;
            var progression = new EndlessBossProgressionSnapshot(baseHp, first, interval, hardRound, breatherRound,
                multiplier, hpPerLine, breatherReduction, hardReduction, scale, finalized == 1);
            if (!progression.TryValidate(out _) || (float)start <= 0
                || !Finite(progression.CalculateDefense(EndlessBossRole.Breather))
                || !Finite(progression.CalculateDefense(EndlessBossRole.Hard))) return false;
            var rates = JsonMapper.ToObject(row["CombatExpPerHp"].ToString());
            if (!rates.IsObject || rates.Count == 0) return false;
            var copiedRates = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string key in rates.Keys)
            {
                if (!EndlessConfigurationValidator.IsValidKey(key) || !Read(rates, key, out double rate)
                    || rate > float.MaxValue || (rate > 0 && (float)rate == 0)) return false;
                copiedRates.Add(key, rate);
            }
            if (!Keys(row, "BreatherBossKeys", out var breatherKeys) || !Keys(row, "HardBossKeys", out var hardKeys)) return false;
            var assigned = new HashSet<string>(breatherKeys, StringComparer.Ordinal);
            foreach (string key in hardKeys) if (!assigned.Add(key)) return false;
            foreach (string key in assigned) if (!copiedRates.ContainsKey(key)) return false;
            snapshot = new EndlessBalanceSnapshot((float)start, hp, (float)breather, (float)hard,
                version, progression, copiedRates, expFirst, expInterval, expGrowth);
            snapshot._breatherBossKeys = Array.AsReadOnly(breatherKeys);
            snapshot._hardBossKeys = Array.AsReadOnly(hardKeys);
            snapshot.ContentHash = UnityEngine.Hash128.Compute(row.ToJson()).ToString();
            return true;
        }
        catch (Exception) { return false; }
    }

    private static bool Read(JsonData row, string key, out double value)
    {
        value = 0;
        var number = row[key];
        if (number == null) return false;
        if (number.IsDouble) value = (double)number;
        else if (!double.TryParse(number.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        return value >= 0 && Finite(value);
    }

    private static bool Integer(JsonData row, string key, out long value)
    {
        value = 0;
        return row[key] != null && long.TryParse(row[key].ToString(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    private static bool Int32(JsonData row, string key, out int value)
    {
        value = 0;
        if (!Integer(row, key, out long number) || number > int.MaxValue) return false;
        value = (int)number;
        return true;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool Keys(JsonData row, string name, out string[] keys)
    {
        keys = row[name].ToString().Split(',');
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = keys[i].Trim();
            if (!EndlessConfigurationValidator.IsValidKey(keys[i]) || !unique.Add(keys[i])) return false;
        }
        return keys.Length > 0;
    }
}
