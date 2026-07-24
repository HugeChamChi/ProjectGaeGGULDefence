using System;
using UnityEngine;

/// <summary>[전사 전용] "정의 구현" 스킬 데미지. "훈련의 성과" 패시브의 투사체 크기 데미지 보너스만
/// projAtkBonusMultiplier배 증폭해서 적용한다(기획 문구: "스킬은 패시브의 데미지 증가가 2배로 적용").</summary>
[Serializable]
public class WarriorJudgementHitEffect : IHitEffect
{
    [Tooltip("기준 데미지(공격력이 아닌 고정 수치). 나머지 보정(크리티컬/토템/부족 등)은 일반 데미지와 동일하게 적용된다.")]
    public float basePower = 200f;
    [Tooltip("훈련의 성과 패시브의 투사체 크기 데미지 보너스 항목에 곱해지는 배율 (2 = 2배 적용)")]
    public float projAtkBonusMultiplier = 2f;

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        int damage = caster.ComputeDamageFrom(basePower, projAtkBonusMultiplier);
        target.TakeDamage(damage, hitPosition);
    }
}
