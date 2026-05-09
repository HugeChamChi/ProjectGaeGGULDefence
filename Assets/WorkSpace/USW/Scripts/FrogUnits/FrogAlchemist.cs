using UnityEngine;

/// <summary>
/// 연금술사 — 스킬 발동 시 확률 배율로 식량 생산 (보스 공격 없음)
///
///   0.1%  → 4배
///   33.3% → 1~3배 랜덤
///   나머지 → 1배
/// </summary>
public class FrogAlchemist : UnitBase
{
    private const float JackpotChance = 0.001f;
    private const float BonusChance   = 0.334f;

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();

        float roll = Random.value;
        float mult = roll < JackpotChance ? 4f
                   : roll < BonusChance   ? Random.Range(1, 4)
                   : 1f;

        _currency.AddCurrency(unitData.foodPerTick * mult * Manager.Buff.FoodAmountMultiplier);
    }
}
