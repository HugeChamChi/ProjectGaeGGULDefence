using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    private Coroutine _cycleCoroutine;
    private bool      _burstActive;

    protected override void ApplyBuff()
    {
        _burstActive      = false;
        _cycleCoroutine   = StartCoroutine(CycleRoutine());
    }

    protected override void RemoveBuff()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }

        if (_burstActive) EndBurst();
    }

    private IEnumerator CycleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_cycleDuration);
            BeginBurst();
            yield return new WaitForSeconds(_burstDuration);
            EndBurst();
        }
    }

    private void BeginBurst()
    {
        if (totemData == null || _burstActive) return;
        _burstActive = true;
        if (totemData.attackBuffAmount > 0f) Manager.Buff.AddAttackBuff(totemData.attackBuffAmount);
        if (totemData.speedBuffAmount  > 0f) Manager.Buff.AddSpeedBuff(totemData.speedBuffAmount);
    }

    private void EndBurst()
    {
        if (totemData == null || !_burstActive) return;
        _burstActive = false;
        if (totemData.attackBuffAmount > 0f) Manager.Buff.RemoveAttackBuff(totemData.attackBuffAmount);
        if (totemData.speedBuffAmount  > 0f) Manager.Buff.RemoveSpeedBuff(totemData.speedBuffAmount);
    }

    public override List<GridCell> GetAffectedCells()
    {
        var list = new List<GridCell>();
        if (CurrentCell == null || totemData == null) return list;

        var pos = CurrentCell.GridPosition;
        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell != null) list.Add(cell);
        }
        return list;
    }

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return;

        bool hasAtk = totemData.attackBuffAmount > 0f;
        bool hasSpd = totemData.speedBuffAmount  > 0f;
        var  pos    = CurrentCell.GridPosition;

        foreach (var offset in totemData.effectRange)
        {
            var cell = Manager.Grid.GetCell(pos.x + offset.x, pos.y + offset.y);
            if (cell == null) continue;

            cell.SetBuffFlags(
                atk: hasAtk || cell.HasAttackBuff,
                spd: hasSpd || cell.HasSpeedBuff);
        }
    }
}
