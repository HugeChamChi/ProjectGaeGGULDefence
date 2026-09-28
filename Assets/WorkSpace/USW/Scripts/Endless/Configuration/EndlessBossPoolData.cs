using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored candidate set for one boss category; each run owns its own remaining order.</summary>
[CreateAssetMenu(fileName = "EndlessBossPoolData", menuName = "Game/Endless/Boss Pool")]
public sealed class EndlessBossPoolData : ScriptableObject
{
    [SerializeField] private string _key;
    [SerializeField] private EndlessBossRole _role;
    [SerializeField] private EndlessBossPoolEntry[] _entries = Array.Empty<EndlessBossPoolEntry>();
    /// <summary>Stable pool key. NORMAL/HARD profiles may reference shared or separate pools.</summary>
    public string Key => _key;
    /// <summary>Breather is the ordinary boss category; Hard is the difficult boss category.</summary>
    public EndlessBossRole Role => _role;
    /// <summary>Candidate definitions; runtime never modifies them.</summary>
    public IReadOnlyList<EndlessBossPoolEntry> Entries => _entries;
}
