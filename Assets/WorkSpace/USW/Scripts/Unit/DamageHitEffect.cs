using System;
using UnityEngine;

/// <summary>적중 시 보스에게 데미지를 주는 효과.</summary>
[Serializable]
public class DamageHitEffect : IHitEffect
{
    [Tooltip("0보다 크면 공격력 대신 이 고정 수치를 기준 데미지로 사용한다 (예: 마법사 화염구 500).")]
    public float fixedPower = 0f;
    [Tooltip("공격력 대비 계수 (1 = 공격력 그대로). fixedPower가 0보다 크면 무시된다.")]
    public float coefficient = 1f;

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        int damage = fixedPower > 0f
            ? caster.ComputeDamageFrom(fixedPower)
            : Mathf.RoundToInt(caster.GetAttackDamage() * coefficient);
        target.TakeDamage(damage, hitPosition);
    }
}
