using UnityEngine;

/// <summary>
/// Pure particle-effect prefab budget. Do not attach to projectiles, units, or gameplay actors.
/// Saturated duplicate visuals may be omitted; attack callbacks and sounds remain with their callers.
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledEffectBudget : MonoBehaviour
{
    private const int DefaultActiveLimit = 16;
    private const int DefaultRetainedLimit = 32;

    [SerializeField, Min(1)] private int _maxActiveInstances = DefaultActiveLimit;
    [SerializeField, Min(1)] private int _maxRetainedInstances = DefaultRetainedLimit;

    /// <summary>Maximum simultaneous instances of this opt-in visual effect.</summary>
    public int MaxActiveInstances => Mathf.Max(1, _maxActiveInstances);

    /// <summary>Maximum inactive visual instances retained for reuse.</summary>
    public int MaxRetainedInstances => Mathf.Max(MaxActiveInstances, _maxRetainedInstances);
}
