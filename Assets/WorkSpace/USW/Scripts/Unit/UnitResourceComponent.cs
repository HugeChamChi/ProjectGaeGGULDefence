using UnityEngine;

public class UnitResourceComponent : MonoBehaviour
{
    /// <summary>식량이 실제 지급됐을 때 발행된다. 연출은 지급량이나 주기를 변경하지 않는다.</summary>
    public event System.Action OnFoodProduced;

    private UnitBase _unit;
    private UnitDependencies _deps;
    private float _foodTimer;

    public void Init(UnitBase unit, UnitDependencies deps)
    {
        _unit = unit;
        _deps = deps;
        _foodTimer = 0f;
    }

    public void TickFoodProduction(float deltaTime)
    {
        if (_deps?.GameManager?.IsFinished == true) return;
        if (_deps?.CurrencyManager == null || _unit == null || _unit.IsStunned || _unit.IsCellSealed || _unit.unitData == null || deltaTime <= 0f) return;

        float cellFoodSpeedBonus = _unit.GetStatBonus(StatKind.FoodSpeed);
        float speedMultiplier = 1f / Mathf.Max(0.1f, 1f + cellFoodSpeedBonus);
        if (!_unit.IsFoodProductionBuffable) speedMultiplier = 1f;

        _foodTimer += deltaTime / speedMultiplier;

        float payoutInterval = Mathf.Max(1f, _unit.FoodPayoutInterval);
        if (_foodTimer < payoutInterval) return;

        float baseAmount = _unit.GetBaseFoodPerSecond();
        if (baseAmount <= 0f)
        {
            _foodTimer = 0f;
            return;
        }

        int elapsedTicks = Mathf.FloorToInt(_foodTimer / payoutInterval);
        _foodTimer -= elapsedTicks * payoutInterval;

        float cellFoodAmountBonus = _unit.GetStatBonus(StatKind.FoodAmount);
        float amountMultiplier = 1f + cellFoodAmountBonus;
        amountMultiplier *= 1f + (_deps.Research?.Get(ResearchStat.FoodProduction) ?? 0f);
        if (!_unit.IsFoodProductionBuffable) amountMultiplier = 1f;

        float amountPerTick = baseAmount * amountMultiplier * payoutInterval;

        if (amountPerTick > 0f)
        {
            for (int i = 0; i < elapsedTicks; i++)
            {
                _deps.CurrencyManager.AddCurrency(amountPerTick);
                _deps.CurrencyFloaterManager?.ReportFoodProduction(amountPerTick);
                OnFoodProduced?.Invoke();
            }
        }
    }

    public float CurrentFoodProductionPerSecond
    {
        get
        {
            if (_unit == null || _unit.unitData == null || _unit.IsCellSealed) return 0f;
            float baseAmount = _unit.GetBaseFoodPerSecond();
            if (baseAmount <= 0f) return 0f;

            float cellFoodAmountBonus = _unit.GetStatBonus(StatKind.FoodAmount);
            float amountMultiplier = 1f + cellFoodAmountBonus;
            amountMultiplier *= 1f + (_deps?.Research?.Get(ResearchStat.FoodProduction) ?? 0f);
            if (!_unit.IsFoodProductionBuffable) amountMultiplier = 1f;

            float amountPerTick = baseAmount * amountMultiplier;

            float cellIntervalBonus = _unit.GetStatBonus(StatKind.FoodSpeed);
            float intervalMultiplier = 1f / Mathf.Max(0.1f, 1f + cellIntervalBonus);
            if (!_unit.IsFoodProductionBuffable) intervalMultiplier = 1f;

            return amountPerTick / intervalMultiplier;
        }
    }
}
