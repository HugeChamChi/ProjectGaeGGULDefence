using System;
using UnityEngine;

/// <summary>One registered boss definition in a category pool; no per-run state or draw weight.</summary>
[Serializable]
public sealed class EndlessBossPoolEntry
{
    [SerializeField] private string _bossKey;
    [SerializeField] private BossData _boss;
    [SerializeField] private bool _enabled;
    /// <summary>Stable registry key resolved by Import.</summary>
    public string BossKey => _bossKey;
    /// <summary>Authored definition, also the runtime identity used to prevent repeats.</summary>
    public BossData Boss => _boss;
    /// <summary>Whether this candidate participates in the shuffled pool.</summary>
    public bool Enabled => _enabled;
}
