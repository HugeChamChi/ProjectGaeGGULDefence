using UnityEngine;
using VContainer;
using System.Collections.Generic;

/// <summary>
/// 끝없는 수확 토템 — 배치되어 있는 동안 일정 주기마다 토템 스스로 식량을 생성한다.
/// 유닛 버프가 아니라 토템 자체가 식량을 만든다 (셀 위 유닛 불필요).
///
/// 생성량/주기: totemData.functions 안의 FoodGeneratorFunction (interval/amount)
/// 회전 불가 (isRotatable=0). 셀 버프 없음 — 범위 칸 없음.
/// </summary>
public class TotemFoodGenerator : TotemBase
{
    [Inject] private CurrencyManager _currencyManager;

    private bool _generating;
    private double _elapsed;
    private const float MinimumInterval = 0.1f;

    protected override void ApplyBuff()
    {
        var gen = totemData.GetFoodGenerator();
        if (gen == null || gen.amount <= 0f)
        {
            Debug.LogWarning($"TotemFoodGenerator({name}): FoodGeneratorFunction이 없거나 amount = 0. TotemData를 확인하세요.");
            return;
        }
        _elapsed = 0;
        _generating = true;
    }

    protected override void RemoveBuff()
    {
        _generating = false;
        _elapsed = 0;
    }

    private void Update()
    {
        if (!_generating) return;
        var gen = totemData?.GetFoodGenerator();
        if (gen == null) return;
        double interval = Mathf.Max(MinimumInterval, gen.interval);
        _elapsed += Time.deltaTime;
        while (_generating && _elapsed >= interval)
        {
            _elapsed -= interval;
            if (gen.amount > 0f) _currencyManager?.AddCurrency(gen.amount);
        }
    }

    // 셀 버프가 없는 토템 — 영향 칸 없음.
    public override List<GridCell> GetAffectedCells() => new List<GridCell>();
    public override void PaintAffectedCells() { }
}
