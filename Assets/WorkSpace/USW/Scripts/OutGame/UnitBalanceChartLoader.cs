using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GGD.BackendCharts;

public static class UnitBalanceChartLoader
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const string ChartName = "DEV_UnitBaseStats";
#else
    public const string ChartName = "LIVE_UnitBaseStats";
#endif
    private static int generation;

    public static async UniTask InitializeAsync(CancellationToken token)
    {
        if (UnitBalanceRuntime.IsLoaded) return;
        int session = generation;
        if (!UnitBalanceRuntime.RequiresRemote) return;
        if (!Backend.IsLogin) throw new InvalidOperationException("Unit chart requires login");
        var owner = Backend.UserInDate;
        var rows = await BackendChartReader.ReadAsync(ChartName, token);
        var json = ChartSnapshotPayload.Decode(rows.ToJson());
        var next = new UnitBalanceSnapshot(UnitBalanceSnapshot.ParseConfig(json));
        if (session != generation || !Backend.IsLogin || Backend.UserInDate != owner || token.IsCancellationRequested) { next.Dispose(); throw new OperationCanceledException(); }
        if (UnitBalanceRuntime.IsLoaded) { next.Dispose(); return; }
        UnitBalanceRuntime.Install(next);
        Debug.Log("[UnitBalance] Loaded " + ChartName + ": " + next.Count + " units");
    }

    public static void Clear() { generation++; UnitBalanceRuntime.Clear(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Clear();
}
