using System.Collections.Generic;
using UnityEngine;

/// <summary>The prototype reward/spawn catalog. Legacy SOs are never candidates.</summary>
public static class LevelTotemCatalog
{
    private static TotemData[] all;
    public static IReadOnlyList<TotemData> All => all ??= Resources.LoadAll<TotemData>("LevelTotems");
}
