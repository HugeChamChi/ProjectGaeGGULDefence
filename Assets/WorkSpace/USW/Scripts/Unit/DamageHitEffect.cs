using System;
using UnityEngine;

/// <summary>적중 시 보스에게 데미지를 주는 효과.</summary>
[Serializable]
[DisplayName("데미지")]
public class DamageHitEffect : IEffect
{
    [Tooltip("0보다 크면 공격력 대신 이 고정 수치를 기준 데미지로 사용한다 (예: 마법사 화염구 500). 등급별로 다르면 등급별값을 선택.")]
    [KoreanLabel("고정 데미지")]
    [SerializeReference, SelectableReference]
    public IScaledFloat fixedPower = new ConstantFloat();
    [Tooltip("공격력 대비 계수 (1 = 공격력 그대로). fixedPower가 0보다 크면 무시된다.")]
    [KoreanLabel("공격력 계수")]
    [SerializeReference, SelectableReference]
    public IScaledFloat coefficient = new ConstantFloat { value = 1f };

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        var tier = caster.currentTier;
        float power = fixedPower.Get(tier);
        int damage = power > 0f
            ? caster.ComputeDamageFrom(power)
            : Mathf.RoundToInt(caster.GetAttackDamage() * coefficient.Get(tier));
        target.TakeDamage(damage, hitPosition);
    }
}
