using System;
using System.Collections.Generic;
using System.Globalization;
using LitJson;
using UnityEngine;

// Base-stat definitions only. Account levels, purchases and preview growth are separate.
public sealed class UnitBalanceSnapshot : IDisposable
{
    [Serializable] public sealed class Config { public int SchemaVersion; public Definition[] Units; }
    [Serializable] public sealed class Definition { public string UnitKey, AssetKey, Name; public Stats[] Stats; }
    [Serializable] public sealed class Stats
    {
        public string Tier;
        public float Attack, AttackFrequency, SkillCooldown, FoodProduction;
        public int MaxDroneCount;
    }
    private readonly Dictionary<string, Definition> definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<UnitData, UnitData> copies = new();
    private readonly HashSet<UnitData> resolved = new();
    public int Count => definitions.Count;
    public string GetUnitKey(string assetKey) => definitions.TryGetValue(assetKey,out var value) ? value.UnitKey : assetKey;

    public static Config ParseConfig(string json)
    {
        var root = JsonMapper.ToObject(json);
        var units = new List<Definition>();
        foreach (JsonData unit in Array(root, "Units"))
        {
            var stats = new List<Stats>();
            foreach (JsonData stat in Array(unit, "Stats"))
                stats.Add(new Stats { Tier = Text(stat, "Tier"), Attack = Number(stat, "Attack"),
                    AttackFrequency = Number(stat, "AttackFrequency"), SkillCooldown = Number(stat, "SkillCooldown"),
                    FoodProduction = Number(stat, "FoodProduction"), MaxDroneCount = Integer(stat, "MaxDroneCount") });
            units.Add(new Definition { UnitKey = Text(unit, "UnitKey"), AssetKey = Text(unit, "AssetKey"),
                Name = Text(unit, "Name"), Stats = stats.ToArray() });
        }
        return new Config { SchemaVersion = Integer(root, "SchemaVersion"), Units = units.ToArray() };
    }

    private static JsonData Field(JsonData row, string key)
    {
        if (row == null || !row.IsObject || !row.ContainsKey(key) || row[key] == null) throw new ArgumentException("Missing unit field: " + key);
        return row[key];
    }
    private static JsonData Array(JsonData row, string key)
    { var value = Field(row, key); if (!value.IsArray) throw new ArgumentException("Expected unit array: " + key); return value; }
    private static string Text(JsonData row, string key)
    { var value = Field(row, key); if (!value.IsString) throw new ArgumentException("Expected unit string: " + key); return value.ToString(); }
    private static int Integer(JsonData row, string key)
    {
        var value = Field(row, key);
        if (!(value.IsInt || value.IsLong) || !int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            throw new ArgumentException("Expected unit integer: " + key);
        return number;
    }
    private static float Number(JsonData row, string key)
    {
        var value = Field(row, key);
        if (value.IsInt) return (int)value;
        if (value.IsLong) return (long)value;
        if (value.IsDouble) return (float)(double)value;
        throw new ArgumentException("Expected unit number: " + key);
    }

    public UnitBalanceSnapshot(Config config)
    {
        if (config == null || config.SchemaVersion != 1 || config.Units == null || config.Units.Length == 0)
            throw new ArgumentException("Invalid unit base-stat configuration");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var unit in config.Units)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitKey) || string.IsNullOrWhiteSpace(unit.AssetKey)
                || string.IsNullOrWhiteSpace(unit.Name) || !keys.Add(unit.UnitKey) || definitions.ContainsKey(unit.AssetKey)
                || unit.Stats == null || unit.Stats.Length != 4) throw new ArgumentException("Invalid/duplicate unit mapping");
            var tiers = new HashSet<Tier>();
            foreach (var stat in unit.Stats)
            {
                if (stat == null || !tiers.Add(ParseTier(stat.Tier)) || stat.MaxDroneCount < 0) throw new ArgumentException("Invalid unit tier");
                foreach (float value in new[] {stat.Attack, stat.AttackFrequency, stat.SkillCooldown, stat.FoodProduction})
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) throw new ArgumentException("Invalid unit stat");
            }
            definitions.Add(unit.AssetKey, unit);
        }
    }

    private static Tier ParseTier(string tier) => tier switch {
        "normal" => Tier.Normal, "rare" => Tier.Rare, "epic" => Tier.Epic, "legend" => Tier.Legend,
        _ => throw new ArgumentException("Unsupported unit tier: " + tier)
    };

    public UnitData Resolve(UnitData source)
    {
        if (source == null) return null;
        if (resolved.Contains(source)) return source;
        if (!definitions.TryGetValue(source.name, out var definition)) throw new InvalidOperationException("Missing remote unit mapping: " + source.name);
        if (!copies.TryGetValue(source, out var copy))
        {
            copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name; copy.hideFlags = HideFlags.HideAndDontSave;
            copy.unitName = definition.Name;
            copy.atk = new PerTierFloat(); copy.attackSpeed = new PerTierFloat();
            copy.skillCooldown = new PerTierFloat(); copy.foodProduction = new PerTierFloat(); copy.maxDroneCount = new PerTierInt();
            foreach (var stat in definition.Stats)
            {
                var tier = ParseTier(stat.Tier);
                copy.atk.Set(tier, stat.Attack); copy.attackSpeed.Set(tier, stat.AttackFrequency);
                copy.skillCooldown.Set(tier, stat.SkillCooldown); copy.foodProduction.Set(tier, stat.FoodProduction);
                copy.maxDroneCount.Set(tier, stat.MaxDroneCount);
            }
            copies.Add(source, copy); resolved.Add(copy);
        }
        // Asset loading belongs to the original and its lifecycle manager.
        copy.icon = source.icon; copy.prefab = source.prefab;
        copy.LevelBonuses = UnitBonusChart.Resolve(definition.UnitKey, source.LevelBonuses);
        return copy;
    }

    public void Dispose()
    {
        foreach (var copy in copies.Values)
            if (Application.isPlaying) UnityEngine.Object.Destroy(copy); else UnityEngine.Object.DestroyImmediate(copy);
        copies.Clear(); resolved.Clear(); definitions.Clear();
    }
}
