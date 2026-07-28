using System;

/// <summary>AtkCoefficientDamage에 "패시브 데미지 증가 배율 조정" 한 항목만 추가한 변형.
/// 정의구현처럼 "패시브의 데미지 증가가 N배로 적용된다"는 기획 문구가 있는 스킬 전용.</summary>
[Serializable]
[DisplayName("공격력 계수 데미지 (패시브 증폭)")]
public class AtkCoefficientDamageWithPassiveBonus : AtkCoefficientDamage
{
    [KoreanLabel("패시브 보너스 배율")]
    public float passiveBonusMultiplier = 1f;

    protected override float PassiveBonusMultiplier => passiveBonusMultiplier;
}
