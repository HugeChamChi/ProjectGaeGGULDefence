using System;
using UnityEngine;

namespace GGD.Events
{
    // Configuration is supplied by the caller after loading the planner JSON/remote chart.
    // No client-side reward payment or implicit persistence is performed here.
    public static class EventDataBridge
    {
        public static event Action<DataOccurrence> Occurred;
        public static EventDataRuntime Current { get; private set; }
        private static string account;
        private static Func<DateTimeOffset> clock;
        public static bool IsConfiguredFor(string accountKey) => Current != null && account == accountKey;

        public static void Configure(string accountKey, string json, int commonResetHourKst, Func<DateTimeOffset> serverClock)
        {
            if (serverClock == null) throw new ArgumentNullException(nameof(serverClock));
            var config = JsonUtility.FromJson<EventDataConfig>(json);
            var next = new EventDataRuntime(accountKey, config, commonResetHourKst);
            account = accountKey; clock = serverClock; Current = next;
        }
        public static void Clear() { Current = null; account = null; clock = null; }
        public static void EnsureConfigured(string accountKey)
        {
            if (Current != null && account == accountKey) return;
            Clear();
            if (string.IsNullOrEmpty(accountKey)) return;
            var asset = Resources.Load<TextAsset>("EventDataConfig");
            if (asset == null) return;
            Configure(accountKey, asset.text, DailyManager.ResetHourKst, () => {
                if (!ServerClock.IsSynced) throw new InvalidOperationException("Event progress requires synchronized server time.");
                return new DateTimeOffset(ServerClock.UtcNow, TimeSpan.Zero);
            });
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Clear(); Occurred = null; }

        public static void BattleCompleted(string accountKey, string runKey, bool hard, double seconds, int round)
        {
            if (string.IsNullOrEmpty(accountKey) || string.IsNullOrEmpty(runKey)) return;
            // An old account's runtime must never consume a new account's result.
            EnsureConfigured(accountKey);
            var occurrence = new DataOccurrence {
                AccountKey = accountKey, OccurrenceKey = runKey,
                Hook = hard ? EventHookKey.BattleEnd_Hard : EventHookKey.BattleEnd_Normal,
                OccurredAt = ServerClock.IsSynced ? new DateTimeOffset(ServerClock.UtcNow,TimeSpan.Zero) : throw new InvalidOperationException("Server time required."),
                PlaySeconds = seconds, ReachedRound = round
            };
            Current?.Apply(occurrence);
            if (BackEnd.Backend.IsLogin && accountKey == BackEnd.Backend.UserInDate) Player.PlayerData?.RecordEventBattle(occurrence);
            Occurred?.Invoke(occurrence);
        }
    }
}
