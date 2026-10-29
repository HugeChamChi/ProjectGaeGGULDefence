using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored milestone. Runtime parts contain no account, UI or backend dependencies.</summary>
[Serializable]
public sealed class UnitLevelBonusDefinition
{
    [Min(1)] public int Level = 1;
    public string Description;
    [SerializeReference, SelectableReference] public List<UnitLevelBonusPart> Parts = new();
}

[Serializable]
public abstract class UnitLevelBonusPart { }
