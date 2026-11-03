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
        private static string configuredOwner;
        public static void Clear() { generation++; configuredOwner=null; EventDataBridge.Clear(); AttendanceEventRuntime.Clear(); EventMissions.Clear(); }
        [Serializable] private sealed class Settings { public bool UseRemote; }
        public static async UniTask InitializeAsync(string owner)
        {
            if (configuredOwner == owner) return;
            int session = generation;
            var settings = Resources.Load<TextAsset>("EventChartSettings");
            if (settings == null || !JsonUtility.FromJson<Settings>(settings.text).UseRemote)
            { var local = Resources.Load<TextAsset>("EventDataConfig"); if (local != null) AttendanceEventRuntime.Configure(local.text); EventDataBridge.EnsureConfigured(owner); return; }
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime);
            var events = await BackendChartReader.ReadAsync(ChartName.Replace("MissionEvents", "Events"), timeout.Token);
            var missions = await BackendChartReader.ReadAsync(ChartName.Replace("MissionEvents", "Missions"), timeout.Token);
            if (session != generation || !Backend.IsLogin || owner != Backend.UserInDate) throw new OperationCanceledException();
            EventMissions.Configure(events.ToJson(), missions.ToJson());
            configuredOwner=owner;
            if (ServerClock.IsSynced) Player.PlayerData?.VisitEventMissions(new DateTimeOffset(ServerClock.UtcNow,TimeSpan.Zero));
        }
    }
}
