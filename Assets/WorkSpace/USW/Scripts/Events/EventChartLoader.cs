using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GGD.BackendCharts;

namespace GGD.Events
{
    public static class EventChartLoader
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const string ChartName = "DEV_MissionEvents";
#else
        public const string ChartName = "LIVE_MissionEvents";
#endif
        private static int generation;
        public static void Clear() { generation++; EventDataBridge.Clear(); }
        [Serializable] private sealed class Settings { public bool UseRemote; }
        public static async UniTask InitializeAsync(string owner)
        {
            if (EventDataBridge.IsConfiguredFor(owner)) return;
            int session = generation;
            var settings = Resources.Load<TextAsset>("EventChartSettings");
            if (settings == null || !JsonUtility.FromJson<Settings>(settings.text).UseRemote)
            { EventDataBridge.EnsureConfigured(owner); return; }
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime);
            var rows = await BackendChartReader.ReadAsync(ChartName, timeout.Token);
            var json = ChartSnapshotPayload.Decode(rows.ToJson());
            if (session != generation || !Backend.IsLogin || owner != Backend.UserInDate) throw new OperationCanceledException();
            EventDataBridge.Configure(owner, json, DailyManager.ResetHourKst, () => {
                if (!ServerClock.IsSynced) throw new InvalidOperationException("Event progress requires synchronized server time.");
                return new DateTimeOffset(ServerClock.UtcNow, TimeSpan.Zero);
            });
        }
    }
}
