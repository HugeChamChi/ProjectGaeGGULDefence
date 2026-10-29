using System;
using UnityEngine;

// Gameplay reads validated data here; it never needs the backend SDK or a network loader.
public static class UnitBalanceRuntime
{
    [Serializable] private sealed class Settings { public bool UseRemote; }
    private static UnitBalanceSnapshot snapshot;
    private static bool? remoteRequired;
    public static bool IsLoaded => snapshot != null;

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

    public static UnitData Resolve(UnitData source)
    {
        if (snapshot != null) return snapshot.Resolve(source);
        if (RequiresRemote) throw new InvalidOperationException("Remote unit base stats must load before creating units or opening the upgrade screen");
        return source;
    }

    internal static void Install(UnitBalanceSnapshot value) => snapshot = value;
    internal static void Clear()
    {
        snapshot?.Dispose();
        snapshot = null;
        remoteRequired = null;
    }
}
