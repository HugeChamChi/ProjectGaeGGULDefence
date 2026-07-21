using UnityEngine;

public class UnitResourceComponent : MonoBehaviour
{
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
        if (_deps?.CurrencyManager == null || _unit == null || _unit.unitData == null || deltaTime <= 0f) return;

        float cellFoodSpeedBonus = _unit.GetStatBonus(StatKind.FoodSpeed);
        float speedMultiplier = 1f / Mathf.Max(0.1f, 1f + cellFoodSpeedBonus);
        if (!_unit.IsFoodProductionBuffable) speedMultiplier = 1f;

        _foodTimer += deltaTime / speedMultiplier;

        if (_foodTimer < 1f) return;

        float baseAmount = _unit.GetBaseFoodPerSecond();
        if (baseAmount <= 0f)
        {
            _foodTimer = 0f;
            return;
        }

        int elapsedTicks = Mathf.FloorToInt(_foodTimer);
        _foodTimer -= elapsedTicks;

        float cellFoodAmountBonus = _unit.GetStatBonus(StatKind.FoodAmount);
        float chieftainFoodBonus = (_deps?.ChieftainManager != null && _deps.ChieftainManager.ChieftainUnit == _unit) ? (_deps.LevelUpManager?.ChieftainFoodProductionBonus ?? 0f) : 0f;
        float amountMultiplier = 1f + cellFoodAmountBonus + chieftainFoodBonus;
        if (!_unit.IsFoodProductionBuffable) amountMultiplier = 1f;

        float amountPerTick = baseAmount * amountMultiplier;

        if (amountPerTick > 0f)
        {
            for (int i = 0; i < elapsedTicks; i++)
            {
                _deps.CurrencyManager.AddCurrency(amountPerTick);
                _deps.CurrencyFloaterManager?.SpawnCurrencyText(transform.position + Vector3.up * 0.5f, amountPerTick);
            }
        }
    }

    public float CurrentFoodProductionPerSecond
    {
        get
        {
            if (_unit == null || _unit.unitData == null) return 0f;
            float baseAmount = _unit.GetBaseFoodPerSecond();
            if (baseAmount <= 0f) return 0f;

            float cellFoodAmountBonus = _unit.GetStatBonus(StatKind.FoodAmount);
            float chieftainFoodBonus = (_deps?.ChieftainManager != null && _deps.ChieftainManager.ChieftainUnit == _unit) ? (_deps.LevelUpManager?.ChieftainFoodProductionBonus ?? 0f) : 0f;
            float amountMultiplier = 1f + cellFoodAmountBonus + chieftainFoodBonus;
            if (!_unit.IsFoodProductionBuffable) amountMultiplier = 1f;

            float amountPerTick = baseAmount * amountMultiplier;

            float cellIntervalBonus = _unit.GetStatBonus(StatKind.FoodSpeed);
            float intervalMultiplier = 1f / Mathf.Max(0.1f, 1f + cellIntervalBonus);
            if (!_unit.IsFoodProductionBuffable) intervalMultiplier = 1f;

            return amountPerTick / intervalMultiplier;
        }
    }
}
