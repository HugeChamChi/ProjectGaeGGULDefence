using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

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
    [SerializeField] protected TotemData     totemData;
    [SerializeField] private   SpriteRenderer _spriteRenderer;
    [SerializeField] private   Image          _image;

    public TotemData Data        => totemData;

    /// <summary>소환 직후 SO 데이터를 주입한다. TotemSpawner에서 호출.</summary>
    public void SetTotemData(TotemData data) => totemData = data;
    public bool      IsActive    { get; private set; } = false;
    public int       RotationStep { get; private set; } = 0;

    public GridCell CurrentCell { get; private set; }

    protected virtual void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_image == null)
            _image = GetComponentInChildren<Image>();
    }

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

        UpdateSprite();
        ApplyBuff();

        // 토템 목록에 등록 (FindObjectsOfType 대체)
        Manager.Buff.RegisterTotem(this);
        Manager.Buff.RebuildCellBuffFlags();
        Manager.Population?.Add(1);

        Debug.Log($"[토템] {totemData.totemName} 배치 @ {cell.GridPosition}");
    }

    public void OnRemoved()
    {
        if (!IsActive) return;

        if (Manager.Grid != null && Manager.Grid.IsPreviewingTotem(this))
            Manager.Grid.ClearTotemRangePreview();

        IsActive    = false;
        CurrentCell = null;

        RemoveBuff();

        // 토템 목록에서 해제
        Manager.Buff.UnregisterTotem(this);
        Manager.Buff.RebuildCellBuffFlags();
        Manager.Population?.Remove(1);

        Debug.Log($"[토템] {totemData?.totemName} 제거");
    }

    // ── 회전 ───────────────────────────────────────────────────

    /// <summary>클릭 시 90° CW 회전. 스프라이트를 다음 회전 이미지로 교체하고 버프 플래그 재계산.</summary>
    public void Rotate()
    {
        if (!IsActive) return;
        RotationStep = (RotationStep + 1) % 4;
        UpdateSprite();
        Manager.Buff.RebuildCellBuffFlags();

        if (Manager.Grid != null && Manager.Grid.IsPreviewingTotem(this))
            Manager.Grid.ShowTotemRangePreview(this);
    }

    private void UpdateSprite()
    {
        if (totemData == null) return;
        var arr = totemData.rotationSprites;
        Sprite s = totemData.DisplaySprite;
        if (arr != null && arr.Length > RotationStep) s = arr[RotationStep];
        if (s == null) return;

        if (_spriteRenderer != null)
            _spriteRenderer.sprite = s;

        if (_image != null)
        {
            _image.sprite = s;
            _image.preserveAspect = true;
        }
    }

    /// <summary>effectRange 오프셋에 현재 RotationStep만큼 90° CW 회전 적용.</summary>
    public Vector2Int RotateOffset(Vector2Int offset)
    {
        var o = offset;
        for (int i = 0; i < RotationStep; i++)
            o = new Vector2Int(o.y, -o.x);
        return o;
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
