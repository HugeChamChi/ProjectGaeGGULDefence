using UnityEngine;
using VContainer;
using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// 끝없는 수확 토템 — 배치되어 있는 동안 일정 주기마다 토템 스스로 식량을 생성한다.
/// 유닛 버프가 아니라 토템 자체가 식량을 만든다 (셀 위 유닛 불필요).
///
/// 생성량  : totemData.foodAmountBuffAmount (시트 FoodAmount, 예: 30)
/// 생성 주기: totemData.foodGenInterval (SO, 기본 10초)
/// 회전 불가 (isRotatable=0). 셀 버프 없음 — 범위 칸 없음.
/// </summary>
public class TotemFoodGenerator : TotemBase
{
    [Inject] private CurrencyManager _currencyManager;

    private CancellationTokenSource _genCts;

    protected override void ApplyBuff()
    {
        if (totemData.foodAmountBuffAmount <= 0f)
        {
            Debug.LogWarning($"TotemFoodGenerator({name}): foodAmountBuffAmount = 0. TotemData를 확인하세요.");
            return;
        }
        StartGeneration();
    }

    protected override void RemoveBuff()
    {
        StopGeneration();
    }

    private void StartGeneration()
    {
        StopGeneration();
        _genCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        GenerateLoopAsync(_genCts.Token).Forget();
    }

    private void StopGeneration()
    {
        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = null;
    }

    private async UniTaskVoid GenerateLoopAsync(CancellationToken token)
    {
        float interval = Mathf.Max(0.1f, totemData != null ? totemData.foodGenInterval : 10f);
        try
        {
            while (true)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);

                float amount = totemData != null ? totemData.foodAmountBuffAmount : 0f;
                if (amount > 0f)
                    _currencyManager?.AddCurrency(amount);
            }
        }
        catch (OperationCanceledException) { }
    }

    // 셀 버프가 없는 토템 — 영향 칸 없음.
    public override List<GridCell> GetAffectedCells() => new List<GridCell>();
    public override void PaintAffectedCells() { }
}
