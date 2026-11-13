using System;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>
    /// Boss pattern numbers from the BACKND chart DEV_/LIVE_BossPatterns (columns Key, Value; TODO G12, user answer Q10 2026-11-11:
    /// every value that changes the fight is in the chart, values that only show something stay in the assets).
    /// Excel source: Data/Balance/BossPatterns.xlsx. Keys are <c>&lt;asset name&gt;.&lt;value&gt;</c>, for example <c>LizardThreat.Interval</c>.
    /// The value name is the field name of the pattern asset with a capital first letter.
    /// Animation lengths, hold frames, shake, priority and the moment inside a Spine animation where an effect lands stay in the assets.
    ///
    /// Built to grow with new bosses (user request 2026-11-13). Nothing here names a boss or an asset:
    /// <list type="bullet">
    /// <item>Which values a pattern has is decided by its kind: <see cref="PatternValues"/> per <see cref="BossPatternType"/> and
    /// <see cref="MechanicValues"/> per <see cref="PatternLabMechanic"/>. A new pattern kind adds one line there.</item>
    /// <item>The allowed range of a value is decided by its name: <see cref="ValueRules"/>. A new value name adds one line there.</item>
    /// <item>The key list (BossPatternsKeys.cs) is generated from the pattern assets by the menu
    /// Tools/Balance/BossPatterns 키 목록 다시 만들기. A new boss needs only its assets, that menu and the new rows in the workbook and charts.</item>
    /// </list>
    /// A pattern asset without keys (tutorial, lab) keeps its bundled values.
    /// The game never writes the assets: <see cref="Runtime"/> hands the pattern controller copies that carry the chart numbers.
    /// </summary>
    public static class BossPatterns
    {
        public const string Thresholds = "Thresholds";

        /// <summary>Allowed range of each value name, shared by every pattern that has it.</summary>
        public static readonly IReadOnlyDictionary<string, BalanceRule> ValueRules = new Dictionary<string, BalanceRule>
        {
            // BossPatternData
            { "Interval", new BalanceRule(1f, 600f) },              // seconds between casts (Timer and Both triggers)
            { "HpThreshold", new BalanceRule(0.01f, 0.99f) },       // HP fraction that triggers the cast (Phase and Both triggers)
            { "ImpactTimeSec", new BalanceRule(0.1f, 10f) },        // warning before the effect lands
            { "Duration", new BalanceRule(0f, 60f) },               // how long the effect lasts
            { "DamageReductionRate", new BalanceRule(0f, 1f) },
            { "SpeedReductionRate", new BalanceRule(0f, 0.95f) },
            { "RandomCellCount", new BalanceRule(1f, 24f, wholeNumber: true) },
            // LizardPatternData
            { "ShieldFraction", new BalanceRule(0f, 1f) },
            { "HealDelay", new BalanceRule(0f, 60f) },
            { "HealFraction", new BalanceRule(0f, 1f) },
            // PlacementBossPhaseData
            { Thresholds, new BalanceRule(0.01f, 0.99f, BalanceShape.List) },
            // PatternLabData
            { "Windup", new BalanceRule(0.5f, 30f) },
            { "ActiveDuration", new BalanceRule(0.1f, 60f) },
            { "StunDuration", new BalanceRule(0f, 30f) },
            { "EffectDuration", new BalanceRule(0.1f, 60f) },
            { "SlowRate", new BalanceRule(0f, 0.9f) },
            { "TetherDistance", new BalanceRule(1f, 10f) },
            { "BlastRadius", new BalanceRule(1f, 6f, wholeNumber: true) },
            { "SafeColumns", new BalanceRule(1f, 6f, wholeNumber: true) },
            { "MemorySafeCount", new BalanceRule(1f, 24f, wholeNumber: true) },
            { "LockTime", new BalanceRule(0.1f, 60f) },
            { "BeamWidthCells", new BalanceRule(0.1f, 6f) },
        };

        /// <summary>Effect values of each pattern kind. Interval and HpThreshold are added from the trigger, not listed here.</summary>
        private static readonly Dictionary<BossPatternType, string[]> PatternValues = new Dictionary<BossPatternType, string[]>
        {
            { BossPatternType.DamageReduction, new[] { "DamageReductionRate", "Duration" } },
            { BossPatternType.SpeedReduction, new[] { "SpeedReductionRate", "Duration" } },
            { BossPatternType.AttackDisable, new[] { "Duration" } },
            { BossPatternType.DestroyUnit, new string[0] },
            { BossPatternType.SealCell, new[] { "Duration" } },
            { BossPatternType.Earthquake, new[] { "ImpactTimeSec", "Duration" } },
            { BossPatternType.LizardThreat, new[] { "SpeedReductionRate", "Duration" } },
            { BossPatternType.LizardThornBarrier, new[] { "DamageReductionRate", "ShieldFraction", "Duration" } },
            { BossPatternType.LizardBurrow, new[] { "Duration", "HealDelay", "HealFraction" } },
            // Placement skills only pick a mechanic; their numbers are the HP gates and the mechanics themselves.
            { BossPatternType.PlacementMechanic, new string[0] },
        };

        /// <summary>Values of each placement mechanic (only the ones that mechanic reads).</summary>
        private static readonly Dictionary<PatternLabMechanic, string[]> MechanicValues = new Dictionary<PatternLabMechanic, string[]>
        {
            { PatternLabMechanic.BreakTether, new[] { "Windup", "StunDuration", "TetherDistance" } },
            { PatternLabMechanic.GapWall, new[] { "Windup", "StunDuration", "ActiveDuration" } },
            { PatternLabMechanic.StormShelter, new[] { "Windup", "StunDuration", "SafeColumns" } },
            { PatternLabMechanic.CarrierBomb, new[] { "Windup", "StunDuration", "BlastRadius" } },
            { PatternLabMechanic.BaitLaser, new[] { "Windup", "StunDuration", "LockTime", "BeamWidthCells" } },
            { PatternLabMechanic.MemoryTiles, new[] { "Windup", "LockTime", "MemorySafeCount", "SlowRate", "EffectDuration" } },
            { PatternLabMechanic.OccupyRelay, new[] { "Windup", "StunDuration" } },
            { PatternLabMechanic.RedLight, new[] { "Windup", "StunDuration", "ActiveDuration" } },
        };

        private static readonly Dictionary<string, BalanceRule> RuleTable = BuildRules();
        private static readonly Dictionary<UnityEngine.Object, Copy> Copies = new Dictionary<UnityEngine.Object, Copy>();

        public static readonly BalanceChart Chart = new BalanceChart("BossPatterns", "BossPatternsChartSettings", RuleTable, extraCheck: CheckAcrossKeys);

        private sealed class Copy { public UnityEngine.Object Object; public float[] Signature; }

        private static Dictionary<string, BalanceRule> BuildRules()
        {
            var rules = new Dictionary<string, BalanceRule>();
            foreach (string key in BossPatternsKeys.Keys)
            {
                string name = key.Substring(key.LastIndexOf('.') + 1);
                if (!ValueRules.TryGetValue(name, out var rule)) throw new InvalidOperationException("BossPatterns: no rule for value name " + name + " (key " + key + ")");
                rules.Add(key, rule);
            }
            return rules;
        }

        // Checks that hold for any boss: HP gates fire from high to low, a mechanic locks before it resolves, a heal happens while the effect lasts.
        private static string CheckAcrossKeys(Dictionary<string, float[]> values)
        {
            foreach (var pair in values)
            {
                int dot = pair.Key.LastIndexOf('.');
                string asset = pair.Key.Substring(0, dot), name = pair.Key.Substring(dot + 1);
                if (name == Thresholds)
                {
                    if (pair.Value.Length == 0) return pair.Key + " needs at least one value";
                    for (int i = 1; i < pair.Value.Length; i++)
                        if (pair.Value[i] >= pair.Value[i - 1]) return pair.Key + " must decrease";
                }
                else if (name == "LockTime" && values.TryGetValue(asset + ".Windup", out var windup) && pair.Value[0] >= windup[0])
                    return pair.Key + " must be shorter than " + asset + ".Windup";
                else if (name == "HealDelay" && values.TryGetValue(asset + ".Duration", out var duration) && pair.Value[0] > duration[0])
                    return pair.Key + " must not exceed " + asset + ".Duration";
            }
            return null;
        }

        /// <summary>Value names the kind of <paramref name="asset"/> has (empty for other objects). The chart holds them only when the key list lists the asset.</summary>
        public static IReadOnlyList<string> Names(UnityEngine.Object asset)
        {
            switch (asset)
            {
                case BossPatternData pattern:
                    var names = new List<string>();
                    bool placement = pattern.patternType == BossPatternType.PlacementMechanic;
                    if (pattern.triggerType != PatternTriggerType.Phase) names.Add("Interval");
                    if (pattern.triggerType != PatternTriggerType.Timer && !placement) names.Add("HpThreshold");
                    if (PatternValues.TryGetValue(pattern.patternType, out var effect)) names.AddRange(effect);
                    if (pattern.rangeType == PatternRangeType.Random && !placement && IsCellPattern(pattern.patternType)) names.Add("RandomCellCount");
                    return names;
                case PatternLabData mechanic:
                    return MechanicValues.TryGetValue(mechanic.Mechanic, out var values) ? values : Array.Empty<string>();
                case PlacementBossPhaseData _:
                    return new[] { Thresholds };
                default:
                    return Array.Empty<string>();
            }
        }

        private static bool IsCellPattern(BossPatternType type)
            => type == BossPatternType.DamageReduction || type == BossPatternType.SpeedReduction || type == BossPatternType.AttackDisable
               || type == BossPatternType.DestroyUnit || type == BossPatternType.SealCell;

        /// <summary>Field of an asset that a chart value name stands for (the name itself or with a lower-case first letter).</summary>
        public static FieldInfo Field(Type type, string name)
            => type.GetField(name, BindingFlags.Public | BindingFlags.Instance)
               ?? type.GetField(char.ToLowerInvariant(name[0]) + name.Substring(1), BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Patterns to register for a boss: the given assets when no chart number differs, otherwise copies carrying the chart numbers
        /// (including shared HP gates and placement mechanics). Copies are reused until the chart values change.
        /// </summary>
        public static BossPatternData[] Runtime(BossPatternData[] patterns)
        {
            if (patterns == null) return null;
            var result = new BossPatternData[patterns.Length];
            for (int i = 0; i < patterns.Length; i++) result[i] = patterns[i] == null ? null : (BossPatternData)Current(patterns[i]);
            return result;
        }

        private static UnityEngine.Object Current(UnityEngine.Object source)
        {
            var signature = Signature(source);
            if (signature == null) return source;
            if (Copies.TryGetValue(source, out var copy) && copy.Object != null && Same(copy.Signature, signature)) return copy.Object;
            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = source.name;
            clone.hideFlags = HideFlags.DontSave;
            Write(clone, source);
            if (clone is PlacementBossPatternData placement)
            {
                if (placement.Phases != null) placement.Phases = (PlacementBossPhaseData)Current(placement.Phases);
                if (placement.Candidates != null)
                {
                    placement.Candidates = (PatternLabData[])placement.Candidates.Clone();
                    for (int i = 0; i < placement.Candidates.Length; i++)
                        if (placement.Candidates[i] != null) placement.Candidates[i] = (PatternLabData)Current(placement.Candidates[i]);
                }
            }
            if (copy != null && copy.Object != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(copy.Object); else UnityEngine.Object.DestroyImmediate(copy.Object);
            }
            Copies[source] = new Copy { Object = clone, Signature = signature };
            return clone;
        }

        // Chart numbers of the asset and of the assets it points at. Null = nothing differs from the assets, use the asset itself.
        private static float[] Signature(UnityEngine.Object source)
        {
            var numbers = new List<float>();
            bool differs = Collect(source, numbers);
            if (source is PlacementBossPatternData placement)
            {
                if (placement.Phases != null) differs |= Collect(placement.Phases, numbers);
                if (placement.Candidates != null)
                    foreach (var candidate in placement.Candidates) if (candidate != null) differs |= Collect(candidate, numbers);
            }
            return differs ? numbers.ToArray() : null;
        }

        private static bool Collect(UnityEngine.Object source, List<float> numbers)
        {
            bool differs = false;
            foreach (string name in Names(source))
            {
                string key = source.name + "." + name;
                if (!RuleTable.ContainsKey(key)) continue;
                var field = Field(source.GetType(), name);
                if (field.FieldType == typeof(float[]))
                {
                    var bundled = (float[])field.GetValue(source) ?? Array.Empty<float>();
                    var list = Chart.List(key, bundled);
                    numbers.Add(list.Length);
                    numbers.AddRange(list);
                    differs |= !Same(list, bundled);
                }
                else
                {
                    float bundled = Convert.ToSingle(field.GetValue(source));
                    float value = Chart.Number(key, bundled);
                    numbers.Add(value);
                    differs |= value != bundled;
                }
            }
            return differs;
        }

        private static void Write(UnityEngine.Object target, UnityEngine.Object source)
        {
            foreach (string name in Names(source))
            {
                string key = source.name + "." + name;
                if (!RuleTable.ContainsKey(key)) continue;
                var field = Field(target.GetType(), name);
                if (field.FieldType == typeof(float[])) field.SetValue(target, Chart.List(key, (float[])field.GetValue(target)).Clone());
                else
                {
                    float value = Chart.Number(key, Convert.ToSingle(field.GetValue(target)));
                    field.SetValue(target, field.FieldType == typeof(int) ? (object)Mathf.RoundToInt(value) : value);
                }
            }
        }

        private static bool Same(float[] left, float[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        public static string ChartName => Chart.ChartName;
        public static IReadOnlyDictionary<string, BalanceRule> Rules => Chart.Rules;
        public static bool IsLoaded => Chart.IsLoaded;
        /// <inheritdoc cref="BalanceChart.Parse"/>
        public static Dictionary<string, float[]> Parse(JsonData rows) => Chart.Parse(rows);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Chart.Clear(); Copies.Clear(); }
    }
}
