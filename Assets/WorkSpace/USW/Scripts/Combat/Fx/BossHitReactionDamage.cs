using System;
using UnityEngine;

/// <summary>Runs an authored damage effect once, then requests a heavy pose only when that hit removed living HP.</summary>
[Serializable]
[DisplayName("피해 후 보스 큰 피격 반응")]
public sealed class BossHitReactionDamage : AtkCoefficientDamage
{
    /// <inheritdoc />
    public override void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        if (target == null) return;
        decimal before = target.CurrentHp;
        base.Apply(caster, target, hitPosition);
        React(target, hitPosition, before);
    }

    /// <inheritdoc />
    public override Action<BossBase, Vector3> Capture(UnitBase caster)
    {
        var damage = base.Capture(caster);
        return (target, position) =>
        {
            if (target == null) return;
            decimal before = target.CurrentHp;
            damage(target, position);
            React(target, position, before);
        };
    }

    private static void React(BossBase target, Vector3 hitPosition, decimal before)
    {
        if (!target.IsDead && target.CurrentHp < before)
            target.GetComponent<BossHitReaction>()?.HeavyImpact(hitPosition);
    }
}
