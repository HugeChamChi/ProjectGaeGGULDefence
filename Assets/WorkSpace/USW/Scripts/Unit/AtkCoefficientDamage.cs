using System;
using UnityEngine;

/// <summary>적중 시 공격력 계수를 기준 데미지로 보스에게 준다 (예: 사격 공격력 100%, 정의구현 공격력 200%).
/// passiveBonusMultiplier: "훈련의 성과" 등 패시브의 데미지 증가 항목에만 곱해지는 배율
/// (기본 1, 정의구현처럼 패시브 증가분만 2배로 받는 스킬만 조정).</summary>
[Serializable]
[DisplayName("공격력 계수 데미지")]
public class AtkCoefficientDamage : IEffect
{
    [KoreanLabel("공격력 계수")]
    [SerializeReference, SelectableReference]
    public IScaledFloat coefficient = new ConstantFloat { value = 1f };

    [Tooltip("패시브의 데미지 증가 항목에만 곱해지는 배율 (기본 1)")]
    [KoreanLabel("패시브 보너스 배율")]
    public float passiveBonusMultiplier = 1f;

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        float baseDamage = caster.GetUpgradedAtk() * coefficient.Get(caster.currentTier);
        int damage = caster.ComputeDamageFrom(baseDamage, passiveBonusMultiplier);
        target.TakeDamage(damage, hitPosition);
    }
}
