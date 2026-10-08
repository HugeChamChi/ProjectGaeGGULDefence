using System;
using System.Collections.Generic;

/// <summary>Consumes Disigman's charge, restores time immediately and fires one authored hit.</summary>
[Serializable]
[DisplayName("디시그망 시간 회복 사격")]
public sealed class DisigmanSkillAction : ISkillAction
{
    /// <inheritdoc />
    public void Execute(UnitBase caster, UnitCombatComponent combat, List<IEffect> hitEffects,
        List<IAdditionalEffect> additionalEffects)
    {
        if (caster is Disigman disigman && combat != null && disigman.TryCast())
            combat.LaunchProjectile(hitEffects, additionalEffects);
    }
}
