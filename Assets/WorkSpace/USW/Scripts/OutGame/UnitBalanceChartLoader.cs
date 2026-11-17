using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GGD.BackendCharts;

public static class UnitBalanceChartLoader
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD || INTERNAL_TEST_BUILD
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
        if (!OutgameBalance.IsLoaded) throw new InvalidOperationException("OutgameBalance must load before unit prices");
        var next = ParseAndValidate(json, OutgameBalance.Integer(OutgameBalance.DeckUpgradeMaxLevel, GaeGGUL.DeckUpgrade.DeckUpgradeProgress.MaxLevel));
        if (session != generation || !Backend.IsLogin || Backend.UserInDate != owner || token.IsCancellationRequested) { next.Dispose(); throw new OperationCanceledException(); }
        if (UnitBalanceRuntime.IsLoaded) { next.Dispose(); return; }
        UnitBalanceRuntime.Install(next);
        Debug.Log("[UnitBalance] Loaded " + ChartName + ": " + next.Count + " units");
    }

    private static UnitBalanceSnapshot ParseAndValidate(string json, int maxLevel)
    {
        var next = new UnitBalanceSnapshot(UnitBalanceSnapshot.ParseConfig(json));
        try
        {
            // Validate IDs actually authored in the cost rows. Table.Character contains legacy test data,
            // not the UnitData/ChieftainData used by the account upgrade screen.
            next.ValidateUpgradeCosts(maxLevel);
            return next;
        }
        catch { next.Dispose(); throw; }
    }

    public static void Clear() { generation++; UnitBalanceRuntime.Clear(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Clear();
}
