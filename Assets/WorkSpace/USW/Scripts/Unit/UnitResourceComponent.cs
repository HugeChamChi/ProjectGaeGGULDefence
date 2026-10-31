using UnityEngine;

public class UnitResourceComponent : MonoBehaviour
{
    /// <summary>식량이 실제 지급됐을 때 발행된다. 연출은 지급량이나 주기를 변경하지 않는다.</summary>
    public event System.Action OnFoodProduced;

    private UnitBase _unit;
    private UnitDependencies _deps;
    private float _foodTimer;
    // The normal timer freezes during batch mode, then resumes when the effect ends.
    // Batch credit survives temporary removal/replacement on the same unit instance.
    // Selling/merging destroys this component and discards unpaid credit, as before.
    private float _batchTimer;
    private float _pendingFood;

    public void Init(UnitBase unit, UnitDependencies deps)
    {
        _unit = unit;
        _deps = deps;
        _foodTimer = 0f;
        _batchTimer = 0f;
        _pendingFood = 0f;
    }

    /// <summary>
    /// Accrues the producer's combined own/fleet rate and owns its payment clock.
    /// Call only from the active producer's fleet tick. Stun affects the supplied rate,
    /// while removal/seal pauses the clock. Ending batch mode flushes earned credit
    /// on the next eligible tick; ending the run never pays further credit.
    /// </summary>
    public void TickBatchedFoodProduction(float deltaTime, float foodPerSecond)
    {
        if (_deps?.CurrencyManager == null || _deps.GameManager?.IsFinished == true || _unit == null) return;
        if (!_unit.UsesExternalFoodPayout)
        {
            PayPendingFood();
            _batchTimer = 0f;
            return;
        }
        if (deltaTime <= 0f || _unit.currentCell == null || _unit.IsCellSealed) return;
        float period = Mathf.Max(1f, _unit.FoodPayoutInterval);
        while (deltaTime > 0f)
        {
            float step = Mathf.Min(deltaTime, period - _batchTimer);
            _pendingFood += foodPerSecond * step;
            _batchTimer += step;
            deltaTime -= step;
            if (_batchTimer >= period)
            {
                _batchTimer = 0f;
                PayPendingFood();
            }
        }
    }

    private void PayPendingFood()
    {
        float amount = _pendingFood;
        _pendingFood = 0f;
        PayFood(amount);
    }

    private void PayFood(float amount)
    {
        if (amount <= 0f) return;
        _deps.CurrencyManager.AddCurrency(amount);
        _deps.CurrencyFloaterManager?.ReportFoodProduction(amount);
        OnFoodProduced?.Invoke();
    }

    public void TickFoodProduction(float deltaTime)
    {
        if (_deps?.GameManager?.IsFinished == true) return;
        if (_unit != null && _unit.UsesExternalFoodPayout) return;
        if (_deps?.CurrencyManager == null || _unit == null || _unit.IsStunned || _unit.IsCellSealed || _unit.unitData == null || deltaTime <= 0f) return;

        float cellFoodSpeedBonus = _unit.GetStatBonus(StatKind.FoodSpeed);
        float speedMultiplier = 1f / Mathf.Max(0.1f, 1f + cellFoodSpeedBonus);
        if (!_unit.IsFoodProductionBuffable) speedMultiplier = 1f;

        _foodTimer += deltaTime / speedMultiplier * (1f + _unit.LevelStat(UnitBonusStat.FoodSpeedPercent));

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
                PayFood(amountPerTick * _unit.NextFoodPayoutMultiplier());
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

            return amountPerTick / intervalMultiplier * (1f + _unit.LevelStat(UnitBonusStat.FoodSpeedPercent));
        }
    }
}
