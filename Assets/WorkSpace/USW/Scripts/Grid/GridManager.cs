using UnityEngine;
using System.Collections.Generic;
// ════════════════════════════════════════════════════════
// GridManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
public class GridManager : MonoBehaviour
{
    [SerializeField] private GameConfig       config;
    [SerializeField] private GameObject       cellPrefab;
    [SerializeField] private Transform        gridRoot;
    [SerializeField] private Vector2          cellSpacing = Vector2.zero; // 셀 사이의 추가 간격
    [SerializeField] private List<GridCell>   prebuiltCells = new List<GridCell>(); // 에디터에서 생성된 타일들

    private GridCell[,] _grid;
    private UnitStatusOverlay _unitStatusOverlay;
    [SerializeField, Tooltip("미지정 시 Resources/UnitStatus 공통 프리팹 사용")]
    private UnitStatusGraphic _unitStatusPrefab;

    /// <summary>유닛의 발밑 상태 표시를 그리드 수명의 공용 UI에 연결합니다.</summary>
    public void RegisterUnitStatus(UnitBase unit)
    {
        if (_unitStatusOverlay == null) _unitStatusOverlay = UnitStatusOverlay.Create(transform, _unitStatusPrefab);
        _unitStatusOverlay.Register(unit);
    }

    private TotemBase _previewedTotem;
    private static readonly int RangeTimeId = Shader.PropertyToID("_TotemRangeUnscaledTime");
    private static readonly int DropTimeId = Shader.PropertyToID("_UnitDropUnscaledTime");
    private void LateUpdate()
    {
        Shader.SetGlobalFloat(RangeTimeId, Time.unscaledTime);
        Shader.SetGlobalFloat(DropTimeId, Time.unscaledTime);
    }

    public int Columns => config != null ? config.gridColumns : 0;
    public int Rows    => config != null ? config.gridRows    : 0;

    public void Init()
    {
        if (_grid != null) return; // Already initialized

        if (config     == null) { Debug.LogError("GridManager: config 미연결");     return; }
        if (cellPrefab == null) { Debug.LogError("GridManager: cellPrefab 미연결"); return; }
        // gridRoot는 선택적, 없으면 transform을 사용

        BuildGrid();
    }

    protected void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        DragHandler.OnUnitClickedEvent += OnUnitClicked;
        DragHandler.OnDragStartedEvent += OnDragStarted;
    }

    private void OnDisable()
    {
        DragHandler.OnUnitClickedEvent -= OnUnitClicked;
        DragHandler.OnDragStartedEvent -= OnDragStarted;
    }

    private void OnUnitClicked(UnitBase _) => ClearTotemRangePreview();
    private void OnDragStarted() => ClearTotemRangePreview();

    private Vector2 GetCellVisualSize()
    {
        if (cellPrefab != null)
        {
            var sr = cellPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                // 스프라이트의 실제 월드 크기 계산 (부모 스케일 반영)
                Vector3 scale = sr.transform.lossyScale;
                return new Vector2(sr.sprite.bounds.size.x * scale.x, sr.sprite.bounds.size.y * scale.y);
            }
        }
        float defaultSize = config != null ? config.cellSize : 1.5f;
        return new Vector2(defaultSize, defaultSize);
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Grid In Editor")]
    public void GenerateGridInEditor()
    {
        if (config == null || cellPrefab == null)
        {
            Debug.LogError("GridManager: config 또는 cellPrefab이 없습니다.");
            return;
        }

        Transform parent = gridRoot != null ? gridRoot : transform;

        // 기존 생성된 타일 전부 삭제
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(parent.GetChild(i).gameObject);
        }
        prebuiltCells.Clear();

        Vector2 baseSize = GetCellVisualSize();
        float stepX = baseSize.x + cellSpacing.x;
        float stepY = baseSize.y + cellSpacing.y;

        float startX = -(config.gridColumns - 1) * stepX / 2f;
        float startZ = -(config.gridRows - 1) * stepY / 2f;

        for (int z = 0; z < config.gridRows; z++)
        {
            for (int x = 0; x < config.gridColumns; x++)
            {
                // PrefabUtility.InstantiatePrefab을 사용해 프리팹 연결 유지
                GameObject go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(cellPrefab, parent);
                go.transform.localPosition = new Vector3(startX + x * stepX, startZ + z * stepY, 0f);
                go.name = $"GridCell_{x}_{z}";

                var cell = go.GetComponent<GridCell>();
                prebuiltCells.Add(cell);
            }
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("Grid 생성 및 직렬화 완료!");
    }

#endif

    private void BuildGrid()
    {
        _grid = new GridCell[config.gridColumns, config.gridRows];

        // 직렬화된(미리 생성된) 타일이 있다면 런타임 Instantiation 생략
        if (prebuiltCells != null && prebuiltCells.Count > 0)
        {
            int index = 0;
            for (int z = 0; z < config.gridRows; z++)
            {
                for (int x = 0; x < config.gridColumns; x++)
                {
                    if (index < prebuiltCells.Count)
                    {
                        var cell = prebuiltCells[index++];
                        if (cell != null)
                        {
                            cell.Init(new Vector2Int(x, z));
                            _grid[x, z] = cell;
                        }
                    }
                }
            }
            return;
        }

        Vector2 baseSize = GetCellVisualSize();
        float stepX = baseSize.x + cellSpacing.x;
        float stepY = baseSize.y + cellSpacing.y;

        float startX = -(config.gridColumns - 1) * stepX / 2f;
        float startZ = -(config.gridRows - 1) * stepY / 2f;

        for (int z = 0; z < config.gridRows; z++)
        for (int x = 0; x < config.gridColumns; x++)
        {
            var go   = Instantiate(cellPrefab, gridRoot != null ? gridRoot : transform);
            go.transform.localPosition = new Vector3(startX + x * stepX, startZ + z * stepY, 0f);

            var cell = go.GetComponent<GridCell>();

            if (cell == null)
            {
                Debug.LogError("GridManager: cellPrefab에 GridCell 컴포넌트 없음");
                return;
            }

            cell.Init(new Vector2Int(x, z));
            _grid[x, z] = cell;
        }
    }

    public List<GridCell> GetEmptyCells()
    {
        var list = new List<GridCell>();
        foreach (var cell in _grid)
            if (!cell.IsOccupied && cell.Model.IsAvailable) list.Add(cell);
        return list;
    }

    public List<GridCell> GetOccupiedCells()
    {
        var list = new List<GridCell>();
        foreach (var cell in _grid)
            if (cell.IsOccupied) list.Add(cell);
        return list;
    }

    public GridCell GetCell(int x, int z)
    {
        if (x < 0 || x >= config.gridColumns) return null;
        if (z < 0 || z >= config.gridRows)    return null;
        return _grid[x, z];
    }

    public GridCell GetCell(Vector2Int pos) => GetCell(pos.x, pos.y);

    /// <summary>그리드 중앙 셀 반환 — 족장 자동 배치용</summary>
    public GridCell GetCenterCell() => GetCell(Columns / 2, Rows / 2);

    /// <summary>그리드 영역의 기하학적 정중앙 월드 좌표 반환</summary>
    public Vector3 GetAbsoluteCenterPosition()
    {
        if (_grid == null || Columns == 0 || Rows == 0) return transform.position;
        var minCell = GetCell(0, 0);
        var maxCell = GetCell(Columns - 1, Rows - 1);
        if (minCell != null && maxCell != null)
        {
            return (minCell.transform.position + maxCell.transform.position) * 0.5f;
        }
        return gridRoot != null ? gridRoot.position : transform.position;
    }

    public IEnumerable<GridCell> AllCells()
    {
        foreach (var cell in _grid)
            yield return cell;
    }

    /// <summary>보스 스킬 예고(텔레그래프)를 그리드 전체 셀에 표시/해제한다.</summary>
    public void SetBossTelegraphAll(bool visible)
    {
        if (_grid == null) return;
        foreach (var cell in _grid)
            cell?.Model?.SetBossTelegraph(visible);
    }

    public bool IsPreviewingTotem(TotemBase totem)
    {
        return _previewedTotem == totem;
    }

    public void ShowTotemRangePreview(TotemBase totem)
    {
        ClearTotemRangePreview();

        if (totem == null || !totem.IsActive || totem.CurrentCell == null || totem.Data == null)
            return;

        _previewedTotem = totem;
        PaintTotemPreview(totem);
    }

    /// <summary>Paints an inactive placement preview without registering a live totem.</summary>
    public void ShowTotemPlacementPreview(TotemBase preview)
    {
        ClearTotemRangePreview();
        if (preview == null || preview.CurrentCell == null || preview.Data == null) return;
        PaintTotemPreview(preview);
    }

    private void PaintTotemPreview(TotemBase totem)
    {
        if (totem.Data.HasEffectGroups)
        {
            foreach (var group in totem.Data.EffectGroups)
                if (group != null) foreach (var cell in group.GetCells(totem, this))
                    cell.Model.SetTotemRangePreview(true, false, group.Color);
            return;
        }

        // 영향 셀은 토템별 GetAffectedCells()로 계산한다.
        // effectRange를 쓰지 않는 특수 토템(전진배치 등)도 올바르게 칠해진다.
        foreach (var cell in totem.GetAffectedCells())
        {
            if (cell == null) continue;
            cell.Model.SetTotemRangePreview(effectRange: true, disabledRange: false);
        }

    }

    public void ClearTotemRangePreview()
    {
        _previewedTotem = null;

        if (_grid == null) return;

        foreach (var cell in _grid)
        {
            cell?.Model?.ClearTotemRangePreview();
        }
    }
}
