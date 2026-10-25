using System;
using System.Collections.Generic;
using System.Linq;

namespace GGD.Events
{
    [Serializable] public sealed class EventDataConfig
    {
        public int CatalogVersion = EventDataCatalog.Version;
        public DataBinding[] DataBindings = Array.Empty<DataBinding>();
        public MissionEvent[] MissionEvents = Array.Empty<MissionEvent>();
        public MissionDefinition[] Missions = Array.Empty<MissionDefinition>();
    }
    [Serializable] public sealed class DataBinding
    {
        public string BindingKey, HookKey, DataKey, ValueSource;
        public double ConstantValue;
        public bool Enabled;
    }
    [Serializable] public sealed class MissionEvent
    {
        public string EventKey, Name, StartAt, EndAt;
        public bool Enabled;
    }
    [Serializable] public sealed class MissionDefinition
    {
        public string MissionKey, EventKey, Name, DataKey, Period;
        public double Target;
        public MissionReward[] Rewards = Array.Empty<MissionReward>();
    }
    [Serializable] public sealed class MissionReward { public string itemId; public int amount; }
    public sealed class DataOccurrence
    {
        public string AccountKey, OccurrenceKey;
        public EventHookKey Hook;
        public DateTimeOffset OccurredAt;
        public double PlaySeconds, ReachedRound;
    }
    public sealed class DataUpdate
    {
        public EventDataKey DataKey;
        public DataOperation Operation;
        public double Value;
    }

    // One instance per account/configuration. Storage and authoritative reward delivery are adapters.
    public sealed class EventDataRuntime
    {
        private readonly string account;
        private readonly int resetHourKst;
        private readonly EventDataConfig config;
        private readonly ResolvedBinding[] bindings;
        private readonly Dictionary<string, EventDataKey> missionData;
        private readonly HashSet<string> receipts = new HashSet<string>();
        private readonly Dictionary<string, double> progress = new Dictionary<string, double>();
        public EventDataRuntime(string accountKey, EventDataConfig configuration, int commonResetHourKst)
        {
            if (string.IsNullOrWhiteSpace(accountKey)) throw new ArgumentException("Account required.");
            if (commonResetHourKst < 0 || commonResetHourKst > 23) throw new ArgumentOutOfRangeException(nameof(commonResetHourKst));
            Validate(configuration);
            account = accountKey; config = configuration; resetHourKst = commonResetHourKst;
            var catalog = EventDataCatalog.Default;
            bindings = config.DataBindings.Where(binding => binding.Enabled).Select(binding => {
                var hook = catalog.FindHook(binding.HookKey);
                hook.TryResolveSource(binding.ValueSource, out var source);
                return new ResolvedBinding { Hook = hook.Key, Data = catalog.FindData(binding.DataKey), Source = source, Constant = binding.ConstantValue };
            }).ToArray();
            missionData = config.Missions.ToDictionary(mission => mission.MissionKey, mission => catalog.FindData(mission.DataKey).Key);
        }
        private sealed class ResolvedBinding
        {
            public EventHookKey Hook;
            public DataDefinition Data;
            public EventValueSource Source;
            public double Constant;
            public double Read(DataOccurrence occurrence)
            {
                switch (Source)
                {
                    case EventValueSource.Constant: return Constant;
                    case EventValueSource.PlaySeconds: return occurrence.PlaySeconds;
                    case EventValueSource.ReachedRound: return occurrence.ReachedRound;
                    default: throw new ArgumentOutOfRangeException(nameof(Source));
                }
            }
        }
        public static void Validate(EventDataConfig c)
        {
            if (c == null || c.CatalogVersion != EventDataCatalog.Version || c.DataBindings == null || c.MissionEvents == null || c.Missions == null)
                throw new ArgumentException("Unsupported event configuration.");
            Unique(c.DataBindings.Select(x => x.BindingKey)); Unique(c.MissionEvents.Select(x => x.EventKey)); Unique(c.Missions.Select(x => x.MissionKey));
            var targets = new HashSet<string>();
            foreach (var b in c.DataBindings)
            {
                var hook = EventDataCatalog.Default.FindHook(b.HookKey);
                if (hook == null || EventDataCatalog.Default.FindData(b.DataKey) == null || !hook.TryResolveSource(b.ValueSource, out _)
                    || !Finite(b.ConstantValue) || b.ConstantValue < 0)
                    throw new ArgumentException("Invalid data binding: " + b.BindingKey);
                if (b.Enabled && !targets.Add(b.HookKey + "/" + b.DataKey)) throw new ArgumentException("Duplicate hook/data binding.");
            }
            foreach (var e in c.MissionEvents)
                if (!DateTimeOffset.TryParse(e.StartAt, out var start) || !DateTimeOffset.TryParse(e.EndAt, out var end) || start >= end)
                    throw new ArgumentException("Invalid event dates: " + e.EventKey);
            foreach (var m in c.Missions)
                if (!c.MissionEvents.Any(e => e.EventKey == m.EventKey) || EventDataCatalog.Default.FindData(m.DataKey) == null
                    || !Finite(m.Target) || m.Target <= 0 || (m.Period != "Event" && m.Period != "Daily")
                    || m.Rewards == null || m.Rewards.Length == 0 || m.Rewards.Any(r => r == null || r.amount <= 0 || !new[] { "Gold", "Diamond", "Stamina" }.Contains(r.itemId))
                    || m.Rewards.Select(r => r.itemId).Distinct().Count() != m.Rewards.Length)
                    throw new ArgumentException("Invalid mission: " + m.MissionKey);
        }
        private static void Unique(IEnumerable<string> keys)
        {
            var seen = new HashSet<string>();
            foreach (var key in keys) if (string.IsNullOrWhiteSpace(key) || key.Contains("/") || !seen.Add(key)) throw new ArgumentException("Invalid or duplicate key.");
        }
        private static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        private string ProgressKey(MissionDefinition m, DateTimeOffset at) => m.EventKey + "/" + m.MissionKey + "/" +
            (m.Period == "Daily" ? at.ToOffset(TimeSpan.FromHours(9)).AddHours(-resetHourKst).ToString("yyyy-MM-dd") : "Event");
        public double GetProgress(string missionKey, DateTimeOffset at)
        {
            var m = config.Missions.Single(x => x.MissionKey == missionKey);
            return progress.TryGetValue(ProgressKey(m, at), out var value) ? value : 0;
        }
        public IReadOnlyList<DataUpdate> Apply(DataOccurrence occurrence)
        {
            if (occurrence == null || occurrence.AccountKey != account || string.IsNullOrWhiteSpace(occurrence.OccurrenceKey)
                || EventDataCatalog.Default.FindHook(occurrence.Hook) == null || !Finite(occurrence.PlaySeconds) || occurrence.PlaySeconds < 0
                || !Finite(occurrence.ReachedRound) || occurrence.ReachedRound < 0) throw new ArgumentException("Invalid occurrence.");
            // One normal/hard result per run; use the same key on retries.
            if (receipts.Contains(occurrence.OccurrenceKey)) return Array.Empty<DataUpdate>();
            var updates = bindings.Where(binding => binding.Hook == occurrence.Hook).Select(binding => new DataUpdate {
                DataKey = binding.Data.Key, Operation = binding.Data.Operation, Value = binding.Read(occurrence)
            }).ToArray();
            var pending = new Dictionary<string, double>();
            foreach (var e in config.MissionEvents.Where(e => e.Enabled && occurrence.OccurredAt >= DateTimeOffset.Parse(e.StartAt) && occurrence.OccurredAt < DateTimeOffset.Parse(e.EndAt)))
            foreach (var m in config.Missions.Where(m => m.EventKey == e.EventKey))
            foreach (var u in updates.Where(u => u.DataKey == missionData[m.MissionKey]))
            {
                var key = ProgressKey(m, occurrence.OccurredAt);
                progress.TryGetValue(key, out var old);
                var value = u.Operation == DataOperation.Max ? Math.Max(old, u.Value) : old + u.Value;
                if (!Finite(value)) throw new OverflowException("Mission progress overflow.");
                pending[key] = Math.Min(m.Target, value);
            }
            foreach (var pair in pending) progress[pair.Key] = pair.Value;
            receipts.Add(occurrence.OccurrenceKey);
            return updates;
        }
    }
}
