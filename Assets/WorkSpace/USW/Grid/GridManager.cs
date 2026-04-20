using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

// ════════════════════════════════════════════════════════
// GridManager — InGameSingleton 교체
// ════════════════════════════════════════════════════════
public class GridManager : InGameSingleton<GridManager>
{
    [SerializeField] private GameConfig       config;
    [SerializeField] private GameObject       cellPrefab;
    [SerializeField] private GridLayoutGroup  gridLayout;

    private GridCell[,] _grid;

    public int Columns => config != null ? config.gridColumns : 0;
    public int Rows    => config != null ? config.gridRows    : 0;

    protected override void Awake()
    {
        base.Awake();

        if (config     == null) { Debug.LogError("GridManager: config 미연결");     return; }
        if (cellPrefab == null) { Debug.LogError("GridManager: cellPrefab 미연결"); return; }
        if (gridLayout == null) { Debug.LogError("GridManager: gridLayout 미연결"); return; }

        BuildGrid();
    }

    private void BuildGrid()
    {
        _grid = new GridCell[config.gridColumns, config.gridRows];

        for (int z = 0; z < config.gridRows; z++)
        for (int x = 0; x < config.gridColumns; x++)
        {
            var go   = Instantiate(cellPrefab, gridLayout.transform);
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

    public IEnumerable<GridCell> AllCells()
    {
        foreach (var cell in _grid)
            yield return cell;
    }
}
