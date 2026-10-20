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
    [Inject] private BossManager _debuffBossManager;
    [Inject] private ProjectilePool _debuffProjectilePool;
    [Inject] private GameManager _debuffGameManager;
    [Inject] private FieldPauseVisuals _fieldPause;
    [Inject] private CombatSettings _combatSettings;
    private TotemDebuffEmitter _debuffEmitter;

    [SerializeField] protected TotemData     totemData;
    [SerializeField] private   SpriteRenderer _spriteRenderer;

    private bool _isDataCloned = false;
    private TotemData _authoredData;

    public TotemData Data        => totemData;
    /// <summary>개체 제거 뒤에도 투사체·화상 집계가 유지할 원본 SO. 런타임 수치는 Data에서 읽는다.</summary>
    public TotemData DamageSourceData => _authoredData != null ? _authoredData : totemData;

    /// <summary>소환 직후 SO 데이터를 주입한다. TotemSpawner에서 호출.</summary>
    public void SetTotemData(TotemData data) 
    {
        if (data != null)
        {
            _authoredData = data;
            totemData = Instantiate(data);
            totemData.name = data.name + "_Runtime";
            _isDataCloned = true;
        }
    }
    private bool _isPlaced;
    private bool _effectsApplied;
    /// <summary>봉인 여부와 별개인 실제 배치 상태. 정보·회수 UI에 사용한다.</summary>
    public bool IsPlaced => _isPlaced;
    /// <summary>봉인 시 점유와 상호작용은 유지하고 효과만 중단한다.</summary>
    public bool IsActive => _isPlaced && CurrentCell != null && !CurrentCell.Model.IsSealed;
    public int       RotationStep { get; private set; } = 0;
    private int? _previewRotationStep;

    public GridCell CurrentCell { get; private set; }

    protected virtual void Awake()
    {
        if (_authoredData == null && !_isDataCloned) _authoredData = totemData;
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

        CurrentCell = cell;
        _isPlaced = true;
        OnPlacementStarted();
        cell.Model.OnStateChanged += RefreshSealedEffects;
        if (TryGetDebuffBinding(out var binding) && binding.Trigger == DebuffTrigger.ProjectileHit)
        {
            if (_debuffEmitter == null) _debuffEmitter = gameObject.AddComponent<TotemDebuffEmitter>();
            _debuffEmitter.Initialize(this, _debuffBossManager, _debuffProjectilePool, _debuffGameManager, _fieldPause, _combatSettings);
        }

        UpdateSprite();
        RefreshSealedEffects();

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
        if (!_isPlaced) return;

        if (_gridManager != null && _gridManager.IsPreviewingTotem(this))
            _gridManager.ClearTotemRangePreview();

        if (CurrentCell != null) CurrentCell.Model.OnStateChanged -= RefreshSealedEffects;
        _isPlaced = false;
        OnPlacementEnded();
        if (_debuffEmitter != null) _debuffEmitter.enabled = false;
        CurrentCell = null;

        if (_effectsApplied) { _effectsApplied = false; RemoveBuff(); }

        // 토템 목록에서 해제
        if (_totemBuffManager != null)
        {
            _totemBuffManager.UnregisterTotem(this);
            _totemBuffManager.RebuildCellBuffFlags();
        }

        Debug.Log($"[토템] {totemData?.totemName} 제거");
    }

    private void RefreshSealedEffects()
    {
        bool active = IsActive;
        if (active == _effectsApplied) return;
        _effectsApplied = active;
        if (active) ApplyBuff(); else RemoveBuff();
        if (_debuffEmitter != null) _debuffEmitter.enabled = active;
        _totemBuffManager?.RebuildCellBuffFlags();
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

    /// <summary>배치된 토템을 옮기는 중 — 후보 칸에 있다고 가정한 범위만 칠한다. 실제 칸·버프는 바꾸지 않는다.</summary>
    public void PreviewMoveTo(GridCell cell, GridManager grid)
    {
        if (!IsActive || cell == null || grid == null) return;
        var oldCell = CurrentCell;
        CurrentCell = cell;
        try { grid.ShowTotemPlacementPreview(this); }
        finally { CurrentCell = oldCell; }
    }

    /// <summary>Evaluates a disabled preview clone at a candidate cell without placement or buffs.</summary>
    public void PreviewPlacement(TotemData data, GridCell cell, GridManager grid, int rotationStep = 0)
    {
        if (IsActive || cell == null || grid == null) return;
        var oldData = totemData;
        var oldCell = CurrentCell;
        var oldGrid = _gridManager;
        var oldPreviewRotation = _previewRotationStep;
        totemData = data; CurrentCell = cell; _gridManager = grid;
        _previewRotationStep = ((rotationStep % 4) + 4) % 4;
        try { grid.ShowTotemPlacementPreview(this); }
        finally { totemData = oldData; CurrentCell = oldCell; _gridManager = oldGrid; _previewRotationStep = oldPreviewRotation; }
    }

    /// <summary>실제 배치 시작. 봉인 해제와 구분되는 생명주기 훅.</summary>
    protected virtual void OnPlacementStarted() { }
    /// <summary>이동/회수에 의한 실제 배치 종료.</summary>
    protected virtual void OnPlacementEnded() { }

    // ── 자식 구현 ──────────────────────────────────────────────
    protected abstract void ApplyBuff();
    protected abstract void RemoveBuff();
    public abstract void PaintAffectedCells();
    public abstract List<GridCell> GetAffectedCells();

    private void OnDestroy()
    {
        if (_isPlaced) OnRemoved();

        // 런타임에 복제된 ScriptableObject 메모리 해제
        if (_isDataCloned && totemData != null)
        {
            Destroy(totemData);
        }
    }
}
