using System;
using UnityEngine;

/// <summary>[전사 전용] "정의 구현" 스킬 데미지. 공격력 계수를 기준 데미지로 사용하고,
/// "훈련의 성과" 패시브의 투사체 크기 데미지 보너스만 projAtkBonusMultiplier배 증폭해서 적용한다
/// (기획 문구: "공격력의 200%의 피해", "스킬은 패시브의 데미지 증가가 2배로 적용").</summary>
[Serializable]
[DisplayName("전사 정의구현")]
public class WarriorJudgementHitEffect : IEffect
{
    [Tooltip("공격력 대비 계수 (2 = 공격력의 200%). 등급별로 다르면 등급별값을 선택.")]
    [KoreanLabel("공격력 계수")]
    [SerializeReference, SelectableReference]
    public IScaledFloat coefficient = new ConstantFloat { value = 2f };
    [Tooltip("훈련의 성과 패시브의 투사체 크기 데미지 보너스 항목에 곱해지는 배율 (2 = 2배 적용)")]
    [KoreanLabel("데미지 보너스 배율")]
    public float projAtkBonusMultiplier = 2f;

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        float baseDamage = caster.GetUpgradedAtk() * coefficient.Get(caster.currentTier);
        int damage = caster.ComputeDamageFrom(baseDamage, projAtkBonusMultiplier);
        target.TakeDamage(damage, hitPosition);
    }
}
