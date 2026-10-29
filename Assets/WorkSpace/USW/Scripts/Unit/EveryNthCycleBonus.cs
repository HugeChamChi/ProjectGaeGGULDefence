using System;
using UnityEngine;

/// <summary>Modifies a named action cycle. Additional actions do not advance this cycle.</summary>
[Serializable]
public sealed class EveryNthCycleBonus : UnitLevelBonusPart
{
    public string CycleId;
    [Min(1)] public int Every = 2;
    public float DamageBonusRatio;
    public float ChargeTimeMultiplier = 1f;
}
