using UnityEngine;
using VContainer;
using System.Collections.Generic;

/// <summary>
/// 모든 토템의 기반 클래스
/// 
/// 변경 사항:
///   - OnPlaced() → _totemBuffManager.RegisterTotem(this) 추가
///   - OnRemoved() → _totemBuffManager.UnregisterTotem(this) 추가
///   - Manager 통해 접근 통일
/// </summary>

public abstract class TotemBase : MonoBehaviour, IDebuffSource
{
    /// <inheritdoc />
    public bool TryGetDebuffBinding(out DebuffBinding binding)
    { binding = default; return totemData != null && totemData.TryGetDebuffBinding(out binding); }
    [Inject] protected TotemBuffManager _totemBuffManager;
    [Inject] protected GridManager _gridManager;
    [Inject] protected GameDataManager _gameDataManager;
    [Inject] private BossManager _debuffBossManager;
    [Inject] private ProjectilePool _debuffProjectilePool;
    [Inject] private GameManager _debuffGameManager;
    private TotemDebuffEmitter _debuffEmitter;

    [SerializeField] protected TotemData     totemData;
    [SerializeField] private   SpriteRenderer _spriteRenderer;

    private bool _isDataCloned = false;

    public TotemData Data        => totemData;

    /// <summary>소환 직후 SO 데이터를 주입한다. TotemSpawner에서 호출.</summary>
    public void SetTotemData(TotemData data) 
    {
        if (data != null)
        {
            totemData = Instantiate(data);
            totemData.name = data.name + "_Runtime";
            _isDataCloned = true;
        }
    }
    public bool      IsActive    { get; private set; } = false;
    public int       RotationStep { get; private set; } = 0;
    private int? _previewRotationStep;

    public GridCell CurrentCell { get; private set; }

    protected virtual void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    // ── 배치/제거 ──────────────────────────────────────────────

    public void OnPlaced(GridCell cell)
    {
        if (totemData == null)
        {
            Debug.LogError($"TotemBase({name}): totemData 미연결");
            return;
        }

        SyncStatsWithSheet();

        CurrentCell = cell;
        IsActive    = true;
        if (TryGetDebuffBinding(out var binding) && binding.Trigger == DebuffTrigger.ProjectileHit)
        {
            if (_debuffEmitter == null) _debuffEmitter = gameObject.AddComponent<TotemDebuffEmitter>();
            _debuffEmitter.Initialize(this, _debuffBossManager, _debuffProjectilePool, _debuffGameManager);
        }

        UpdateSprite();
        ApplyBuff();

        // 토템 목록에 등록 (FindObjectsOfType 대체)
        if (_totemBuffManager != null)
        {
            _totemBuffManager.RegisterTotem(this);
            _totemBuffManager.RebuildCellBuffFlags();
        }
        else
        {
            Debug.LogWarning($"TotemBase({name}): _totemBuffManager is null!");
        }

        Debug.Log($"[토템] {totemData.totemName} 배치 @ {cell.GridPosition}");
    }

    public void OnRemoved()
    {
        if (!IsActive) return;

        if (_gridManager != null && _gridManager.IsPreviewingTotem(this))
            _gridManager.ClearTotemRangePreview();

        IsActive    = false;
        if (_debuffEmitter != null) _debuffEmitter.enabled = false;
        CurrentCell = null;

        RemoveBuff();

        // 토템 목록에서 해제
        if (_totemBuffManager != null)
        {
            _totemBuffManager.UnregisterTotem(this);
            _totemBuffManager.RebuildCellBuffFlags();
        }

        Debug.Log($"[토템] {totemData?.totemName} 제거");
    }

    // ── 회전 ───────────────────────────────────────────────────

    /// <summary>클릭 시 90° CW 회전. 스프라이트를 다음 회전 이미지로 교체하고 버프 플래그 재계산.</summary>
    public void Rotate()
    {
        SetRotationStep(RotationStep + 1);
    }

    /// <summary>Commits one of four absolute directions and rebuilds actual effects.</summary>
    public void SetRotationStep(int step)
    {
        if (!IsActive || totemData == null || !totemData.isRotatable) return;
        RotationStep = ((step % 4) + 4) % 4;
        UpdateSprite();
        
        if (_totemBuffManager != null)
            _totemBuffManager.RebuildCellBuffFlags();

        if (_gridManager != null && _gridManager.IsPreviewingTotem(this))
            _gridManager.ShowTotemRangePreview(this);
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
    }

    /// <summary>effectRange 오프셋에 현재 RotationStep만큼 90° CW 회전 적용.</summary>
    public Vector2Int RotateOffset(Vector2Int offset)
    {
        var o = offset;
        for (int i = 0; i < (_previewRotationStep ?? RotationStep); i++)
            o = new Vector2Int(o.y, -o.x);
        return o;
    }

    /// <summary>Paints a tentative range without changing sprites, stats or committed rotation.</summary>
    public void PreviewRotation(int step)
    {
        _previewRotationStep = ((step % 4) + 4) % 4;
        try { _gridManager?.ShowTotemRangePreview(this); }
        finally { _previewRotationStep = null; }
    }

    /// <summary>Restores the committed range after cancelling a gesture.</summary>
    public void RestoreRotationPreview() => _gridManager?.ShowTotemRangePreview(this);

    /// <summary>Evaluates a disabled preview clone at a candidate cell without placement or buffs.</summary>
    public void PreviewPlacement(TotemData data, GridCell cell, GridManager grid)
    {
        if (IsActive || cell == null || grid == null) return;
        var oldData = totemData;
        var oldCell = CurrentCell;
        var oldGrid = _gridManager;
        totemData = data; CurrentCell = cell; _gridManager = grid;
        try { grid.ShowTotemPlacementPreview(this); }
        finally { totemData = oldData; CurrentCell = oldCell; _gridManager = oldGrid; }
    }

    // ── 자식 구현 ──────────────────────────────────────────────
    protected abstract void ApplyBuff();
    protected abstract void RemoveBuff();
    public abstract void PaintAffectedCells();
    public abstract List<GridCell> GetAffectedCells();

    protected virtual void SyncStatsWithSheet()
    {
        if (totemData == null || !totemData.UseSheetData || _gameDataManager == null || !_gameDataManager.IsLoaded) return;

        // 원본 ScriptableObject가 오염되는 것을 방지하기 위해 런타임 인스턴스로 복제
        if (!_isDataCloned)
        {
            totemData = Instantiate(totemData);
            totemData.name = totemData.name + "_Runtime";
            _isDataCloned = true;
        }

        var sheetRow = _gameDataManager.GetTotemRow(totemData.totemId);
        if (sheetRow != null)
        {
            totemData.ApplySheetData(sheetRow);
        }
    }

    private void OnDestroy()
    {
        if (IsActive) OnRemoved();

        // 런타임에 복제된 ScriptableObject 메모리 해제
        if (_isDataCloned && totemData != null)
        {
            Destroy(totemData);
        }
    }
}
