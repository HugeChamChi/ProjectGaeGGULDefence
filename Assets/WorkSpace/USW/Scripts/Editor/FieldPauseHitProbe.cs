using UnityEngine;

/// <summary>Counts only the test burst's real projectile impacts, excluding other live drone attacks.</summary>
public sealed class FieldPauseHitProbe : IEffect
{
    private const int ProbeDamage = 10;
    /// <summary>Number of actual arrivals of this effect.</summary>
    public int Hits { get; private set; }
    /// <summary>Accepted damage confirms that counted arrivals actually affect boss HP.</summary>
    public long DamageUnits { get; private set; }

    /// <inheritdoc />
    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        Hits++;
        DamageUnits += target.TakeDamageAndRecord(ProbeDamage, hitPosition);
    }
}
