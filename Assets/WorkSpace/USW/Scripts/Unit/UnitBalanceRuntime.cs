using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gameplay reads validated data here; it never needs the backend SDK or a network loader.
public static class UnitBalanceRuntime
{
    [Serializable] private sealed class Settings { public bool UseRemote; }
    private static UnitBalanceSnapshot snapshot;
    private static bool? remoteRequired;
    private const string TutorialSceneName = "TutorialScene";
    private static bool warnedBundled;
    public static bool IsLoaded => snapshot != null;
    public static UnitBalanceSnapshot.UpgradePrice UpgradeCost(string id, int target) => snapshot != null
        ? snapshot.UpgradeCost(id, Mathf.Min(target, GaeGGUL.DeckUpgrade.DeckUpgradeProgress.CurrentMaxLevel))
        : new UnitBalanceSnapshot.UpgradePrice { Id = id, TargetLevel = target, ItemId = "UpgradeTokenA", Amount = target - 1 };
    public static string GetUnitKey(string assetKey) => snapshot?.GetUnitKey(assetKey) ?? assetKey;

    internal static bool RequiresRemote
    {
        get
        {
            if (!remoteRequired.HasValue)
            {
                var settings = Resources.Load<TextAsset>("UnitBalanceChartSettings");
                remoteRequired = settings != null && JsonUtility.FromJson<Settings>(settings.text).UseRemote;
            }
            return remoteRequired.Value;
        }
    }

    /// <summary>
    /// Chart values when loaded. Bundled asset values in the tutorial and in direct Editor play
    /// (user decision 2026-11-11: only the Editor and the tutorial run on bundled values).
    /// </summary>
    public static UnitData Resolve(UnitData source)
    {
        if (SceneManager.GetActiveScene().name == TutorialSceneName) return source;
        if (snapshot != null) return snapshot.Resolve(source);
        if (RequiresRemote)
        {
            // A Player build cannot get here without a chart: login blocks until the chart is fetched.
            if (!Application.isEditor) throw new InvalidOperationException("Remote unit base stats must load before creating units or opening the upgrade screen");
            // Direct Editor play starts without login, so no chart was fetched.
            if (!warnedBundled)
            {
                warnedBundled = true;
                Debug.LogWarning("[UnitBalance] No chart loaded (Editor play without login): using bundled UnitData values.");
            }
        }
        return source;
    }

    internal static void Install(UnitBalanceSnapshot value) => snapshot = value;
    internal static void Clear()
    {
        snapshot?.Dispose();
        snapshot = null;
        remoteRequired = null;
        warnedBundled = false;
    }
}
