using System;
using UnityEngine;

/// <summary>적중 시 등급별 고정 수치를 기준 데미지로 보스에게 준다 (예: 마법사 화염구 500/1500/2500/3500).</summary>
[Serializable]
[DisplayName("고정 데미지")]
public class FixedDamage : IReplayableEffect
{
    [KoreanLabel("고정 수치")]
    [SerializeReference, SelectableReference]
    public IScaledFloat power = new ConstantFloat();

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        int damage = caster.ComputeDamageFrom(power.Get(caster.currentTier), 1f, out bool critical);
        target.TakeDamage(damage, hitPosition, critical ? BossDamageKind.Critical : BossDamageKind.Normal);
    }

    /// <inheritdoc />
    public Action<BossBase, Vector3> Capture(UnitBase caster)
    {
        int damage = caster.ComputeDamageFrom(power.Get(caster.currentTier), 1f, out bool critical);
        return new RecordedDamage(damage, critical ? BossDamageKind.Critical : BossDamageKind.Normal).Apply;
    }
}
