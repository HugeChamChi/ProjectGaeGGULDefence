using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GGD.BackendCharts
{
    /// <summary>
    /// In-game (totem) level milestones of the five field units, from the BACKND chart DEV_/LIVE_UnitTotemLevelBonuses
    /// (TODO VC8-LT, G12). One row is one part of one milestone. Excel source: Data/Balance/UnitTotemLevelBonuses.xlsx;
    /// the bundled copy is Resources/UnitTotemLevelBonuses.json, made from the same workbook.
    /// Same rules as <see cref="DebuffChart"/>: the running tutorial and direct Editor play use the bundled rows,
    /// a failed or malformed chart blocks login, nothing is applied partially.
    /// </summary>
    public static class UnitTotemLevelBonusChart
    {
        public const string ContentName = "UnitTotemLevelBonuses";
        public static string ChartName => ChartEnvironment.Prefix + ContentName;
        private const string BundledResource = "UnitTotemLevelBonuses";
        private const string SettingsResource = "UnitTotemLevelBonusesChartSettings";
        private const string TutorialSceneName = "TutorialScene";

        /// <summary>Chart columns. Description is read from the PartIndex 0 row of each milestone.</summary>
        public static readonly string[] Columns = { "UnitKey", "Level", "PartIndex", "Stat", "Value", "Description" };

        /// <summary>Unit keys every chart must list, in display order.</summary>
        public static readonly string[] UnitKeys = { "Disigman", "Betan", "Deltan", "Gamman", "Zeltan" };

        /// <summary>Whole-number stats (counts, periods).</summary>
        private static readonly HashSet<UnitBonusStat> WholeStats = new()
        {
            UnitBonusStat.DroneCountFlat, UnitBonusStat.ChargeAttackReductionFlat, UnitBonusStat.HackingCapacityFlat,
            UnitBonusStat.ThirdSkillExtraBomb, UnitBonusStat.ExtraProjectileCount, UnitBonusStat.ExtraProjectileEvery,
            UnitBonusStat.BombCycleEvery
        };

        /// <summary>Ratios that must stay below 1 (100%).</summary>
        private static readonly HashSet<UnitBonusStat> BelowOneStats = new()
        {
            UnitBonusStat.SkillCooldownPercent
        };

        /// <summary>Ratios that may reach 1 (100%) but not more.</summary>
        private static readonly HashSet<UnitBonusStat> AtMostOneStats = new()
        {
            UnitBonusStat.FoodBurstChance, UnitBonusStat.HackingRefundPercent
        };

        /// <summary>Periods and durations: when present they must be at least this.</summary>
        private static readonly Dictionary<UnitBonusStat, float> Minimums = new()
        {
            { UnitBonusStat.ExtraProjectileEvery, 1f }, { UnitBonusStat.BombCycleEvery, 1f },
            { UnitBonusStat.FoodBurstIntervalSeconds, 0.1f }, { UnitBonusStat.FoodBurstDurationSeconds, 0.1f },
            { UnitBonusStat.FoodBurstMultiplier, 1f }
        };

        [Serializable] private sealed class Settings { public bool UseRemote; }

        private static Dictionary<string, List<UnitLevelBonusDefinition>> installed;
        private static Dictionary<string, List<UnitLevelBonusDefinition>> bundled;
        private static bool? remoteRequired;
        private static int generation;

        public static bool IsLoaded => installed != null;

        /// <summary>False until the DEV_ and LIVE_ charts exist on the server; then login fetches and requires them.</summary>
        public static bool RequiresRemote
        {
            get
            {
                if (!remoteRequired.HasValue)
                {
                    var settings = Resources.Load<TextAsset>(SettingsResource);
                    remoteRequired = settings != null && JsonUtility.FromJson<Settings>(settings.text).UseRemote;
                }
                return remoteRequired.Value;
            }
        }

        /// <summary>Unit key of a field unit that uses in-game levels, or null.</summary>
        public static string KeyOf(UnitBase unit) => unit switch
        {
            Disigman => "Disigman",
            Drone_Betan => "Betan",
            Drone_Deltan => "Deltan",
            Drone_Gamman => "Gamman",
            Drone_Zeltan => "Zeltan",
            _ => null
        };

        /// <summary>
        /// Milestones for <paramref name="unit"/>: the installed chart outside the tutorial, otherwise the bundled rows.
        /// Each call returns new definition objects, so callers cannot change the shared table.
        /// </summary>
        public static List<UnitLevelBonusDefinition> For(UnitBase unit)
        {
            string key = KeyOf(unit);
            if (key == null) return new List<UnitLevelBonusDefinition>();
            var source = installed != null && !TutorialRunning() ? installed : Bundled();
            return source.TryGetValue(key, out var list) ? Copy(list) : new List<UnitLevelBonusDefinition>();
        }

        private static bool TutorialRunning() =>
            Application.isPlaying && SceneManager.GetActiveScene().name == TutorialSceneName;

        /// <summary>Bundled rows, parsed once. A broken bundled file is a build error, so it throws.</summary>
        public static Dictionary<string, List<UnitLevelBonusDefinition>> Bundled()
        {
            if (bundled != null) return bundled;
            var text = Resources.Load<TextAsset>(BundledResource);
            if (text == null) throw new InvalidOperationException("Resources/" + BundledResource + ".json is required.");
            bundled = Parse(JsonMapper.ToObject(text.text));
            return bundled;
        }

        /// <summary>
        /// Validates chart rows. Every unit in <see cref="UnitKeys"/> must have at least one milestone.
        /// Throws <see cref="FormatException"/> naming the row and problem.
        /// </summary>
        public static Dictionary<string, List<UnitLevelBonusDefinition>> Parse(JsonData rows)
        {
            if (rows == null || !rows.IsArray || rows.Count == 0) throw new FormatException(ContentName + ": no rows");
            var parts = new Dictionary<(string, int), SortedDictionary<int, StatModifierBonus>>();
            var descriptions = new Dictionary<(string, int), string>();
            var known = new HashSet<string>(UnitKeys);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                string where = ContentName + " row " + (i + 1);
                foreach (var column in Columns)
                    if (!row.IsObject || !row.ContainsKey(column)) throw new FormatException(where + ": " + column + " column missing");
                string key = Text(row, "UnitKey");
                if (!known.Contains(key)) throw new FormatException(where + ": unknown UnitKey " + key);
                int level = Whole(row, "Level", where);
                int index = Whole(row, "PartIndex", where);
                if (level < 1 || level > 1000) throw new FormatException(where + ": Level must be 1-1000");
                if (index < 0 || index > 20) throw new FormatException(where + ": PartIndex must be 0-20");
                string statText = Text(row, "Stat");
                if (!Enum.TryParse(statText, false, out UnitBonusStat stat) || !Enum.IsDefined(typeof(UnitBonusStat), stat)
                    || int.TryParse(statText, out _))
                    throw new FormatException(where + ": unknown Stat " + statText);
                float value = Number(row, "Value", where);
                CheckValue(stat, value, where);
                var group = (key, level);
                if (!parts.TryGetValue(group, out var list)) parts.Add(group, list = new SortedDictionary<int, StatModifierBonus>());
                if (list.ContainsKey(index)) throw new FormatException(where + ": duplicate PartIndex " + index + " for " + key + " Lv" + level);
                list.Add(index, new StatModifierBonus { Stat = stat, Value = value });
                if (index == 0)
                {
                    string description = Text(row, "Description");
                    if (description.Length == 0 || description.Contains("<") || description.Contains(">"))
                        throw new FormatException(where + ": Description is required on PartIndex 0 and must not contain < or >");
                    descriptions[group] = description;
                }
            }

            var result = new Dictionary<string, List<UnitLevelBonusDefinition>>();
            foreach (var pair in parts)
            {
                int expected = 0;
                var definition = new UnitLevelBonusDefinition
                {
                    Level = pair.Key.Item2,
                    BonusId = "TotemLevel_" + pair.Key.Item1 + "_" + pair.Key.Item2,
                    Description = descriptions.TryGetValue(pair.Key, out var text) ? text : ""
                };
                foreach (var part in pair.Value)
                {
                    if (part.Key != expected++) throw new FormatException(ContentName + ": PartIndex gap in " + pair.Key.Item1 + " Lv" + pair.Key.Item2);
                    definition.Parts.Add(part.Value);
                }
                if (!result.TryGetValue(pair.Key.Item1, out var list)) result.Add(pair.Key.Item1, list = new List<UnitLevelBonusDefinition>());
                list.Add(definition);
            }
            foreach (var key in UnitKeys)
            {
                if (!result.TryGetValue(key, out var list)) throw new FormatException(ContentName + ": missing unit " + key);
                list.Sort((a, b) => a.Level.CompareTo(b.Level));
                // Same check the combat controller runs, at the highest level, so a bad table fails here and not mid-battle.
                try { new UnitLevelBonusController(list, list[list.Count - 1].Level); }
                catch (ArgumentException error) { throw new FormatException(ContentName + " " + key + ": " + error.Message); }
            }
            return result;
        }

        private static void CheckValue(UnitBonusStat stat, float value, string where)
        {
            if (value < 0f) throw new FormatException(where + ": Value must not be negative");
            if (WholeStats.Contains(stat) && (value != Mathf.Floor(value) || value > 10000f))
                throw new FormatException(where + ": " + stat + " must be a whole number up to 10000");
            if (BelowOneStats.Contains(stat) && value >= 1f) throw new FormatException(where + ": " + stat + " must be below 1 (100%)");
            if (AtMostOneStats.Contains(stat) && value > 1f) throw new FormatException(where + ": " + stat + " must be at most 1 (100%)");
            if (Minimums.TryGetValue(stat, out float minimum) && value < minimum)
                throw new FormatException(where + ": " + stat + " must be at least " + minimum.ToString(CultureInfo.InvariantCulture));
        }

        private static List<UnitLevelBonusDefinition> Copy(List<UnitLevelBonusDefinition> source)
        {
            var copy = new List<UnitLevelBonusDefinition>(source.Count);
            foreach (var definition in source)
            {
                var next = new UnitLevelBonusDefinition { Level = definition.Level, BonusId = definition.BonusId, Description = definition.Description };
                foreach (var part in definition.Parts)
                    if (part is StatModifierBonus stat) next.Parts.Add(new StatModifierBonus { Stat = stat.Stat, Value = stat.Value });
                copy.Add(next);
            }
            return copy;
        }

        private static string Text(JsonData row, string column) => row[column]?.ToString().Trim() ?? "";

        private static float Number(JsonData row, string column, string where)
        {
            string text = Text(row, column);
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException(where + ": " + column + " is not a number: " + text);
            return value;
        }

        private static int Whole(JsonData row, string column, string where)
        {
            string text = Text(row, column);
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw new FormatException(where + ": " + column + " is not a whole number: " + text);
            return value;
        }

        /// <summary>Fetches and installs the chart during login. Any failure throws and blocks entry.</summary>
        public static async UniTask LoadAsync(CancellationToken token)
        {
            if (IsLoaded || !RequiresRemote) return;
            int session = generation;
            if (!Backend.IsLogin) throw new InvalidOperationException(ContentName + " chart requires login");
            var owner = Backend.UserInDate;
            var rows = await BackendChartReader.ReadAsync(ChartName, token);
            var next = Parse(rows);
            if (session != generation || !Backend.IsLogin || Backend.UserInDate != owner || token.IsCancellationRequested) throw new OperationCanceledException();
            if (IsLoaded) return;
            installed = next;
            Debug.Log("[UnitTotemLevelBonuses] Loaded " + ChartName + ": " + next.Count + " units");
        }

        /// <summary>Installs already validated rows (login loader and Editor checks).</summary>
        public static void Install(Dictionary<string, List<UnitLevelBonusDefinition>> next) => installed = next;

        /// <summary>Forgets the chart: account change, logout and domain reload. The bundled cache is also dropped so a re-import is seen.</summary>
        public static void Clear() { generation++; installed = null; bundled = null; remoteRequired = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Clear();
    }
}
