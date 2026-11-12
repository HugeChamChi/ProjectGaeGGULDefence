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
    /// Debuff definitions from the BACKND chart DEV_/LIVE_Debuffs, one debuff per row (TODO G12).
    /// Excel source: Data/Balance/Debuffs.xlsx. <see cref="DebuffCatalog.TryGet"/> asks here first, so combat and the debuff
    /// info text use the same definition. Same rules as <see cref="BalanceChart"/>: the running tutorial and direct Editor play
    /// keep the bundled DebuffData values; a failed or malformed chart blocks login; nothing is applied partially.
    /// </summary>
    public static class DebuffChart
    {
        public const string ContentName = "Debuffs";
        public static string ChartName => ChartEnvironment.Prefix + ContentName;
        /// <summary>Chart columns. Empty number cells mean 0, except DamageTakenMultiplier which means 1 (no change).</summary>
        public static readonly string[] Columns =
        {
            "DebuffId", "Key", "Kind", "StackGroup", "DurationSeconds", "MaxStacks", "TickIntervalSeconds",
            "SnapshotHpPercent", "MinTickDamage", "ArmorStrength", "DamageTakenMultiplier"
        };

        [Serializable] private sealed class Settings { public bool UseRemote; }
        private const string TutorialSceneName = "TutorialScene";
        private static Dictionary<int, DebuffDefinition> definitions;
        private static bool? remoteRequired;
        private static int generation;

        public static bool IsLoaded => definitions != null;

        public static bool RequiresRemote
        {
            get
            {
                if (!remoteRequired.HasValue)
                {
                    var settings = Resources.Load<TextAsset>("DebuffChartSettings");
                    remoteRequired = settings != null && JsonUtility.FromJson<Settings>(settings.text).UseRemote;
                }
                return remoteRequired.Value;
            }
        }

        /// <summary>Chart definition of <paramref name="id"/>. False when no chart is installed or the tutorial is running: the caller then uses bundled data.</summary>
        public static bool TryGet(int id, out DebuffDefinition definition)
        {
            definition = null;
            if (definitions == null) return false;
            if (Application.isPlaying && SceneManager.GetActiveScene().name == TutorialSceneName) return false;
            return definitions.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Validates chart rows with the same checks as bundled definitions. Every ID in <paramref name="requiredIds"/>
        /// (the bundled debuffs units and totems bind to) must be present. Throws <see cref="FormatException"/> naming the problem.
        /// </summary>
        public static Dictionary<int, DebuffDefinition> Parse(JsonData rows, IEnumerable<int> requiredIds)
        {
            if (rows == null || !rows.IsArray || rows.Count == 0) throw new FormatException("Debuffs: no rows");
            var list = new List<DebuffDefinition>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                string where = "Debuffs row " + (i + 1);
                foreach (var column in Columns)
                    if (!row.IsObject || !row.ContainsKey(column)) throw new FormatException(where + ": " + column + " column missing");
                string key = Text(row, "Key");
                if (!int.TryParse(Text(row, "DebuffId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id <= 0)
                    throw new FormatException(where + ": DebuffId is not a positive whole number");
                if (!Enum.TryParse(Text(row, "Kind"), false, out DebuffKind kind) || !Enum.IsDefined(typeof(DebuffKind), kind) || int.TryParse(Text(row, "Kind"), out _))
                    throw new FormatException(where + ": unknown Kind " + Text(row, "Kind"));
                if (!int.TryParse(Text(row, "MaxStacks"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxStacks))
                    throw new FormatException(where + ": MaxStacks is not a whole number");
                try
                {
                    list.Add(new DebuffDefinition(id, key, kind, Text(row, "StackGroup"), Number(row, "DurationSeconds", 0, where), maxStacks,
                        Number(row, "TickIntervalSeconds", 0, where), (decimal)Number(row, "SnapshotHpPercent", 0, where) / 100,
                        (decimal)Number(row, "MinTickDamage", 0, where), Number(row, "ArmorStrength", 0, where),
                        (decimal)Number(row, "DamageTakenMultiplier", 1, where)));
                }
                catch (ArgumentException error) { throw new FormatException(where + ": " + error.Message); }
            }
            Dictionary<int, DebuffDefinition> result;
            try { result = DebuffCatalog.Validate(list); }
            catch (ArgumentException error) { throw new FormatException("Debuffs: " + error.Message); }
            if (requiredIds != null)
                foreach (int required in requiredIds)
                    if (!result.ContainsKey(required)) throw new FormatException("Debuffs: missing debuff " + required);
            return result;
        }

        private static string Text(JsonData row, string column) => row[column]?.ToString().Trim() ?? "";

        private static double Number(JsonData row, string column, double empty, string where)
        {
            string text = Text(row, column);
            if (text.Length == 0) return empty;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value))
                throw new FormatException(where + ": " + column + " is not a number: " + text);
            return value;
        }

        /// <summary>Fetches and installs the chart during login. Any failure throws and blocks entry.</summary>
        public static async UniTask LoadAsync(CancellationToken token)
        {
            if (IsLoaded || !RequiresRemote) return;
            int session = generation;
            if (!Backend.IsLogin) throw new InvalidOperationException("Debuffs chart requires login");
            var owner = Backend.UserInDate;
            var rows = await BackendChartReader.ReadAsync(ChartName, token);
            var bundled = Resources.Load<DebuffSettings>("DebuffSettings");
            if (bundled == null) throw new InvalidOperationException("Resources/DebuffSettings is required.");
            var required = new List<int>();
            foreach (var definition in bundled.CreateDefinitions()) required.Add(definition.Id);
            var next = Parse(rows, required);
            if (session != generation || !Backend.IsLogin || Backend.UserInDate != owner || token.IsCancellationRequested) throw new OperationCanceledException();
            if (IsLoaded) return;
            definitions = next;
            Debug.Log("[Debuffs] Loaded " + ChartName + ": " + next.Count + " debuffs");
        }

        /// <summary>Installs already validated definitions (login loader and Editor checks).</summary>
        public static void Install(Dictionary<int, DebuffDefinition> next) => definitions = next;

        /// <summary>Forgets the chart: account change, logout and domain reload.</summary>
        public static void Clear() { generation++; definitions = null; remoteRequired = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Clear();
    }
}
