using UnityEngine;

/// <summary>
/// 고블린 마법사
///
/// 패시브: 필드에 배치되는 동안 모든 고블린의 식량 생산량 -20%
/// 공격력: 50
///
/// Inspector 구성:
///   foodReduction 필드로 감소량 조절 가능 (기본 0.2 = 20%)
/// </summary>
public class FrogWizard : UnitBase
{
    [SerializeField, Range(0.01f, 0.9f)]
    [Tooltip("식량 생산량 감소율 (0.2 = 20% 감소)")]
    private float foodReduction = 0.2f;

    protected override void OnUnitPlaced()
    {
        Manager.Buff.AddFoodAmountDebuff(foodReduction);
    }

    protected override void OnUnitRemoved()
    {
        Manager.Buff.RemoveFoodAmountDebuff(foodReduction);
    }
}
