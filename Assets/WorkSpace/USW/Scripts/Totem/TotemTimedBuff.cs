using System;
using VContainer;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// #33 주기적 폭발 버프 토템
///
/// 10초마다 5초간 공격력 + 속도 버프를 전역 적용 후 해제.
/// attackBuffAmount / speedBuffAmount (TotemData)로 수치 제어.
///
/// effectRange 셀은 시각 표시용 (X자 대각).
/// </summary>
public class TotemTimedBuff : TotemBase
{

    [SerializeField] private float _cycleDuration  = 10f;
    [SerializeField] private float _burstDuration  =  5f;

    private CancellationTokenSource _cycleCts;
    private bool                    _burstActive;

    protected override void ApplyBuff()
    {
        _burstActive = false;
        _cycleCts    = new CancellationTokenSource();
        CycleAsync(_cycleCts.Token).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
    }

    protected override void RemoveBuff()
    {
        _cycleCts?.Cancel();
        _cycleCts?.Dispose();
        _cycleCts = null;

        if (_burstActive) EndBurst();
    }

    private async UniTask CycleAsync(CancellationToken token)
    {
        try
        {
            float cycleDuration = Mathf.Max(_cycleDuration, 0.1f);
            float burstDuration = Mathf.Max(_burstDuration, 0.1f);

            while (true)
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Delay(TimeSpan.FromSeconds(cycleDuration), cancellationToken: token);
                token.ThrowIfCancellationRequested();
                BeginBurst();
                await UniTask.Delay(TimeSpan.FromSeconds(burstDuration), cancellationToken: token);
                token.ThrowIfCancellationRequested();
                EndBurst();
            }
        }
        catch (OperationCanceledException)
        {
            if (_burstActive) EndBurst();
        }
    }

    private void BeginBurst()
    {
        if (totemData == null || _burstActive) return;
        _burstActive = true;
        float attackAmount = totemData.GetSimpleAmount(TotemBuffKind.Attack);
        float speedAmount  = totemData.GetSimpleAmount(TotemBuffKind.Speed);
        if (attackAmount > 0f) _totemBuffManager.AddAttackBuff(attackAmount);
        if (speedAmount  > 0f) _totemBuffManager.AddSpeedBuff(speedAmount);
    }

    private void EndBurst()
    {
        if (totemData == null || !_burstActive) return;
        _burstActive = false;
        float attackAmount = totemData.GetSimpleAmount(TotemBuffKind.Attack);
        float speedAmount  = totemData.GetSimpleAmount(TotemBuffKind.Speed);
        if (attackAmount > 0f) _totemBuffManager.RemoveAttackBuff(attackAmount);
        if (speedAmount  > 0f) _totemBuffManager.RemoveSpeedBuff(speedAmount);
    }

    public override List<GridCell> GetAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return new List<GridCell>();
        return totemData.GetEffectCells(this, _gridManager);
    }

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return;

        bool hasAtk = totemData.GetSimpleAmount(TotemBuffKind.Attack) > 0f;
        bool hasSpd = totemData.GetSimpleAmount(TotemBuffKind.Speed)  > 0f;

        foreach (var cell in totemData.GetEffectCells(this, _gridManager))
        {
            cell.SetBuffFlags(
                atk: hasAtk || cell.HasAttackBuff,
                spd: hasSpd || cell.HasSpeedBuff);
        }
    }
}
