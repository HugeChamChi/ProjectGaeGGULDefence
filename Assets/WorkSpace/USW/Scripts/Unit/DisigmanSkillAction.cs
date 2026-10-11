using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Consumes Disigman's charge, restores time immediately and fires one authored hit.</summary>
[Serializable]
[DisplayName("디시그망 시간 회복 사격")]
public sealed class DisigmanSkillAction : ISkillAction
{
    [Tooltip("이 스킬 투사체의 이동/이펙트/프리팹 구성 id (ProjectileData.id, Table.Projectile로 조회). 0이면 ProjectilePool 기본 구성 사용")]
    [SoIdReference(typeof(ProjectileData))]
    public int projectileDataId;
    [Tooltip("이 스킬 투사체에만 적용되는 추가 크기 배율 (1 = 기본)")]
    public float sizeMultiplier = 1f;

    /// <inheritdoc />
    public void Execute(UnitBase caster, UnitCombatComponent combat, List<IEffect> hitEffects,
        List<IAdditionalEffect> additionalEffects)
    {
        if (caster is not Disigman disigman || combat == null || !disigman.TryCast()) return;
        var projectileData = projectileDataId == 0 ? null : Table.Projectile.Get(projectileDataId);
        combat.LaunchProjectile(hitEffects, additionalEffects, sizeMultiplier, projectileData);
    }
}
