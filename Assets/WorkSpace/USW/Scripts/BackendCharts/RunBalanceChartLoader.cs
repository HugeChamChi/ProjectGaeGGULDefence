using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GGD.BackendCharts
{
    /// <summary>Fetches and installs the run balance chart during login. Any failure throws and blocks entry; there is no bundled or cached substitute.</summary>
    public static class RunBalanceChartLoader
    {
        private static int generation;

        public static async UniTask InitializeAsync(CancellationToken token)
        {
            if (RunBalance.IsLoaded || !RunBalance.RequiresRemote) return;
            int session = generation;
            if (!Backend.IsLogin) throw new InvalidOperationException("Run balance chart requires login");
            var owner = Backend.UserInDate;
            var rows = await BackendChartReader.ReadAsync(RunBalance.ChartName, token);
            var next = RunBalance.Parse(rows);
            if (session != generation || !Backend.IsLogin || Backend.UserInDate != owner || token.IsCancellationRequested) throw new OperationCanceledException();
            if (RunBalance.IsLoaded) return;
            RunBalance.Install(next);
            Debug.Log("[RunBalance] Loaded " + RunBalance.ChartName + ": " + next.Count + " keys");
        }

        public static void Clear() { generation++; RunBalance.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Clear();
    }
}
