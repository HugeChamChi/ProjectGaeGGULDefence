using System;
using UnityEngine;

/// <summary>적중 시 공격력 계수를 기준 데미지로 보스에게 준다 (예: 사격 공격력 100%, 정의구현 공격력 200%).</summary>
[Serializable]
[DisplayName("공격력 계수 데미지")]
public class AtkCoefficientDamage : IEffect
{
    [KoreanLabel("공격력 계수")]
    [SerializeReference, SelectableReference]
    public IScaledFloat coefficient = new ConstantFloat { value = 1f };

    /// <summary>"훈련의 성과" 등 패시브의 데미지 증가 항목에만 곱해지는 배율. 기본 1(증폭 없음).</summary>
    protected virtual float PassiveBonusMultiplier => 1f;

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        float baseDamage = caster.GetUpgradedAtk() * coefficient.Get(caster.currentTier);
        int damage = caster.ComputeDamageFrom(baseDamage, PassiveBonusMultiplier);
        target.TakeDamage(damage, hitPosition);
    }
}
