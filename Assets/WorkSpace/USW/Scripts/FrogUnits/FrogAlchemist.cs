using UnityEngine;

/// <summary>
/// 고블린 연금술사
///
/// 식량 생산 시 확률 배율 적용:
///   0.1%  → 4배 (40식량)
///   33.3% → 1~3배 랜덤 (10~30식량)
///   나머지 → 1배 (10식량)
/// 공격력: 0
///
/// 참고: FoodAmountMultiplier(마법사 디버프 등)도 최종 곱에 적용됨
/// </summary>
public class FrogAlchemist : UnitBase
{
    private const float JackpotChance = 0.001f;  // 0.1% — 4배
    private const float BonusChance   = 0.334f;  // 33.3% (누적: jackpot 포함)

    protected override void OnGaugeFull()
    {
        onGaugeFull?.Invoke();

        float roll = Random.value;
        float mult;

        if      (roll < JackpotChance) mult = 4f;
        else if (roll < BonusChance)   mult = Random.Range(1, 4); // 1, 2, 또는 3
        else                           mult = 1f;

        // FoodAmountMultiplier 적용 (마법사 아우라 등)
        _currency.AddCurrency(unitData.foodPerTick * mult * Manager.Buff.FoodAmountMultiplier);
    }
}
