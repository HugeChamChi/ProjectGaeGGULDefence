using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 모든 토템의 기반 클래스
/// 
/// 변경 사항:
///   - OnPlaced() → Manager.Buff.RegisterTotem(this) 추가
///   - OnRemoved() → Manager.Buff.UnregisterTotem(this) 추가
///   - Manager 통해 접근 통일
/// </summary>
public abstract class TotemBase : MonoBehaviour
{
    [SerializeField] protected TotemData totemData;

    public TotemData Data     => totemData;
    public bool      IsActive { get; private set; } = false;

    public GridCell CurrentCell { get; private set; }

    // ── 배치/제거 ──────────────────────────────────────────────

    public void OnPlaced(GridCell cell)
    {
        if (totemData == null)
        {
            Debug.LogError($"TotemBase({name}): totemData 미연결");
            return;
        }

        CurrentCell = cell;
        IsActive    = true;

        ApplyBuff();

        // 토템 목록에 등록 (FindObjectsOfType 대체)
        Manager.Buff.RegisterTotem(this);
        Manager.Buff.RebuildCellBuffFlags();

        Debug.Log($"[토템] {totemData.totemName} 배치 @ {cell.GridPosition}");
    }

    public void OnRemoved()
    {
        if (!IsActive) return;

        IsActive    = false;
        CurrentCell = null;

        RemoveBuff();

        // 토템 목록에서 해제
        Manager.Buff.UnregisterTotem(this);
        Manager.Buff.RebuildCellBuffFlags();

        Debug.Log($"[토템] {totemData?.totemName} 제거");
    }

    // ── 자식 구현 ──────────────────────────────────────────────
    protected abstract void ApplyBuff();
    protected abstract void RemoveBuff();
    public abstract void PaintAffectedCells();
    public abstract List<GridCell> GetAffectedCells();

    private void OnDestroy()
    {
        if (IsActive) OnRemoved();
    }
}
