using System;
using System.Collections.Generic;
using System.Globalization;
using LitJson;
using UnityEngine;

// Base-stat definitions only. Account levels, purchases and preview growth are separate.
public sealed class UnitBalanceSnapshot : IDisposable
{
    [Serializable] public sealed class Config { public int SchemaVersion; public Definition[] Units; public UpgradePrice[] UpgradeCosts; }
    [Serializable] public sealed class Definition { public string UnitKey, AssetKey, Name; public Stats[] Stats; }
    [Serializable] public sealed class Stats
    {
        public string Tier;
        public float Attack, AttackFrequency, SkillCooldown, FoodProduction;
        /// <summary>Betan bomb coefficient, combat drone attack coefficient (same on all four tiers of a unit) and Zeltan food per drone (chart columns added 2026-11-12, TODO G12).</summary>
        public float SelfDestructCoefficient, CombatDroneAttackCoefficient, FleetFoodPerDrone;
        public int MaxDroneCount;
    }
    [Serializable] public sealed class UpgradePrice { public string Id, ItemId; public int TargetLevel, Amount; }
    private readonly Dictionary<string, UpgradePrice> prices = new();
    public UpgradePrice UpgradeCost(string id, int target) => prices.Count == 0
        ? new UpgradePrice { Id = id, TargetLevel = target, ItemId = GaeGGUL.DeckUpgrade.DeckUpgradeCurrency.Token, Amount = target - 1 }
        : prices.TryGetValue(id + "/" + target, out var price) ? price : throw new InvalidOperationException("Missing upgrade cost: " + id + "/" + target);
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
                    FoodProduction = Number(stat, "FoodProduction"), MaxDroneCount = Integer(stat, "MaxDroneCount"),
                    SelfDestructCoefficient = Number(stat, "SelfDestructCoefficient"), CombatDroneAttackCoefficient = Number(stat, "CombatDroneAttackCoefficient"),
                    FleetFoodPerDrone = Number(stat, "FleetFoodPerDrone") });
            units.Add(new Definition { UnitKey = Text(unit, "UnitKey"), AssetKey = Text(unit, "AssetKey"),
                Name = Text(unit, "Name"), Stats = stats.ToArray() });
        }
        return new Config { SchemaVersion = Integer(root, "SchemaVersion"), Units = units.ToArray(), UpgradeCosts = root.ContainsKey("UpgradeCosts") ? Newtonsoft.Json.JsonConvert.DeserializeObject<UpgradePrice[]>(root["UpgradeCosts"].ToJson()) : null };
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
        foreach (var price in config.UpgradeCosts ?? System.Array.Empty<UpgradePrice>())
        {
            if (price == null || string.IsNullOrEmpty(price.Id) || price.TargetLevel < 2 || price.Amount <= 0 || (price.ItemId != "Gold" && price.ItemId != "Diamond" && price.ItemId != "UpgradeTokenA") || !prices.TryAdd(price.Id + "/" + price.TargetLevel, price)) throw new ArgumentException("Invalid upgrade cost");
        }
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
                foreach (float value in new[] {stat.Attack, stat.AttackFrequency, stat.SkillCooldown, stat.FoodProduction,
                    stat.SelfDestructCoefficient, stat.CombatDroneAttackCoefficient, stat.FleetFoodPerDrone})
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) throw new ArgumentException("Invalid unit stat");
                // UnitData holds one combat drone coefficient per unit, so the four tier rows must agree.
                if (stat.CombatDroneAttackCoefficient != unit.Stats[0].CombatDroneAttackCoefficient) throw new ArgumentException("CombatDroneAttackCoefficient differs between tiers: " + unit.UnitKey);
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
        if (!definitions.TryGetValue(source.name, out var definition))
        {
            // A unit with no balance stats at all (the totem joker: it only occupies a cell and merges) is
            // deliberately not a chart row (user decision 2026-11-11). Its prefab loads by address later, so
            // the data itself is the only thing available to recognize it here.
            if (HasNoBalanceStats(source)) return source;
            throw new InvalidOperationException("Missing remote unit mapping: " + source.name);
        }
        if (!copies.TryGetValue(source, out var copy))
        {
            copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name; copy.hideFlags = HideFlags.HideAndDontSave;
            copy.unitName = definition.Name;
            copy.atk = new PerTierFloat(); copy.attackSpeed = new PerTierFloat();
            copy.skillCooldown = new PerTierFloat(); copy.foodProduction = new PerTierFloat(); copy.maxDroneCount = new PerTierInt();
            // The chart is the only source of the bomb coefficient: clear the legacy single-value fallback.
            copy.SelfDestructCoefficients = new PerTierFloat(); copy.SelfDestructAttackCoefficient = 0f; copy.fleetFoodPerDrone = new PerTierFloat();
            copy.CombatDroneAttackCoefficient = definition.Stats[0].CombatDroneAttackCoefficient;
            foreach (var stat in definition.Stats)
            {
                var tier = ParseTier(stat.Tier);
                copy.atk.Set(tier, stat.Attack); copy.attackSpeed.Set(tier, stat.AttackFrequency);
                copy.skillCooldown.Set(tier, stat.SkillCooldown); copy.foodProduction.Set(tier, stat.FoodProduction);
                copy.maxDroneCount.Set(tier, stat.MaxDroneCount);
                copy.SelfDestructCoefficients.Set(tier, stat.SelfDestructCoefficient); copy.fleetFoodPerDrone.Set(tier, stat.FleetFoodPerDrone);
            }
            copies.Add(source, copy); resolved.Add(copy);
        }
        // Asset loading belongs to the original and its lifecycle manager.
        copy.icon = source.icon; copy.prefab = source.prefab;
        copy.LevelBonuses = UnitBonusChart.Resolve(definition.UnitKey, source.LevelBonuses);
        return copy;
    }

    private static bool HasNoBalanceStats(UnitData source)
    {
        foreach (Tier tier in new[] { Tier.Normal, Tier.Rare, Tier.Epic, Tier.Legend })
            if (source.atk.Get(tier) != 0f || source.attackSpeed.Get(tier) != 0f || source.skillCooldown.Get(tier) != 0f
                || source.foodProduction.Get(tier) != 0f || source.maxDroneCount.Get(tier) != 0) return false;
        return true;
    }

    public void Dispose()
    {
        foreach (var copy in copies.Values)
            if (Application.isPlaying) UnityEngine.Object.Destroy(copy); else UnityEngine.Object.DestroyImmediate(copy);
        copies.Clear(); resolved.Clear(); definitions.Clear();
    }
}
