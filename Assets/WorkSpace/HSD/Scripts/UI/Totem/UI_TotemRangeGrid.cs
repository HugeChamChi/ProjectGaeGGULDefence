using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace GaeGGUL.UI.Totem
{
    /// <summary>
    /// 토템의 효과 범위를 시각적으로 보여주는 그리드 컴포넌트입니다.
    /// 범례(Legend)를 슬롯 기반으로 관리합니다.
    /// </summary>
    public class UI_TotemRangeGrid : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private TotemDisplaySettings settings;
        [SerializeField] private Vector2Int gridSize = new Vector2Int(6, 4);
        [SerializeField] private Vector2Int totemPosition = new Vector2Int(4, 2);
        [SerializeField] private UI_TotemRangeCell cellPrefab;
        [SerializeField] private UIGridLayout gridLayout;

        [Header("Legend Settings")]
        [SerializeField] private UI_TotemLegendSlot legendSlotPrefab;
        [SerializeField] private Transform tr_LegendParent;

        private UI_TotemRangeCell[,] _cells;
        private Vector2Int _center;
        private bool _isInitialized = false;

        private void OnEnable()
        {
            if (gridLayout != null)
            {
                gridLayout.OnLayoutChanged -= RefreshLayoutPositions;
                gridLayout.OnLayoutChanged += RefreshLayoutPositions;
            }
        }

        private void OnDisable()
        {
            if (gridLayout != null)
            {
                gridLayout.OnLayoutChanged -= RefreshLayoutPositions;
            }
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터에서 값 변경 시 즉시 반영
            if (_isInitialized)
            {
                UnityEditor.EditorApplication.delayCall += () => {
                    if (this == null) return;
                    RefreshLayoutPositions();
                };
            }
        }
        #endif

        public void Initialize()
        {
            if (settings == null)
            {
                Debug.LogError("[TotemUI] TotemDisplaySettings is missing.");
                return;
            }

            if (gridSize.x <= 0 || gridSize.y <= 0)
            {
                Debug.LogError("[TotemUI] Invalid gridSize.");
                return;
            }

            // 에디터 모드 대응: _cells가 날아갔을 경우 자식들을 확인하여 복구 시도
            if (_cells == null || _cells.Length == 0)
            {
                TryRecoverCells();
            }

            if (_isInitialized && _cells != null && _cells.Length > 0) return;

            // GridLayoutGroup이 있으면 수동 레이아웃을 위해 비활성화
            if (gridLayout != null)
            {
                var glg = gridLayout.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                if (glg != null && glg.enabled) glg.enabled = false;
            }

            SetupLegend();
            SetupGrid();

            _isInitialized = true;
        }

        private void TryRecoverCells()
        {
            if (gridLayout == null) return;

            var existingCells = gridLayout.GetComponentsInChildren<UI_TotemRangeCell>();
            if (existingCells.Length == gridSize.x * gridSize.y)
            {
                _cells = new UI_TotemRangeCell[gridSize.x, gridSize.y];
                int index = 0;
                for (int y = 0; y < gridSize.y; y++)
                {
                    for (int x = 0; x < gridSize.x; x++)
                    {
                        _cells[x, y] = existingCells[index++];
                    }
                }
                _center = new Vector2Int(totemPosition.x - 1, gridSize.y - totemPosition.y);
                _isInitialized = true;
            }
        }

        public void RefreshLayoutPositions()
        {
            if (gridLayout == null || _cells == null || _cells.GetLength(0) != gridSize.x || _cells.GetLength(1) != gridSize.y) 
            {
                _isInitialized = false;
                Initialize();
                if (_cells == null || _cells.GetLength(0) != gridSize.x || _cells.GetLength(1) != gridSize.y) return;
            }

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    var cell = _cells[x, y];
                    if (cell == null) continue;

                    var cellRect = cell.transform as RectTransform;
                    var (pos, size) = gridLayout.GetCellRect(gridSize, new Vector2Int(x, y));
                    gridLayout.ApplyLayoutToChild(cellRect, pos, size);
                }
            }
        }


        private void SetupLegend()
        {
            if (legendSlotPrefab == null || tr_LegendParent == null) return;

            // 기존 자식 오브젝트 정리
            for (int i = tr_LegendParent.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(tr_LegendParent.GetChild(i).gameObject);
                else DestroyImmediate(tr_LegendParent.GetChild(i).gameObject);
            }

            var legendData = new (string label, Sprite sprite)[]
            {
                (settings.fieldLabel, settings.fieldSprite),
                (settings.totemLabel, settings.totemSprite),
                (settings.effectLabel, settings.effectSprite),
                (settings.debuffLabel, settings.debuffSprite)
            };

            foreach (var data in legendData)
            {
                var slot = Instantiate(legendSlotPrefab, tr_LegendParent);
                slot.SetData(data.label, data.sprite);
            }
        }

        private void SetupGrid()
        {
            if (cellPrefab == null || gridLayout == null) return;

            // 기존 그리드 자식 정리
            for (int i = gridLayout.transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(gridLayout.transform.GetChild(i).gameObject);
                else DestroyImmediate(gridLayout.transform.GetChild(i).gameObject);
            }

            _cells = new UI_TotemRangeCell[gridSize.x, gridSize.y];
            _center = new Vector2Int(totemPosition.x - 1, gridSize.y - totemPosition.y);

            for (int y = 0; y < gridSize.y; y++)
            {
                for (int x = 0; x < gridSize.x; x++)
                {
                    var cell = Instantiate(cellPrefab, gridLayout.transform);
                    var cellRect = cell.transform as RectTransform;
                    
                    var (pos, size) = gridLayout.GetCellRect(gridSize, new Vector2Int(x, y));
                    gridLayout.ApplyLayoutToChild(cellRect, pos, size);

                    _cells[x, y] = cell;
                    _cells[x, y].SetSprite(settings.fieldSprite); // 초기화 시 필드 스프라이트
                }
            }
        }

        public void SetRange(List<Vector2Int> effectRange, List<Vector2Int> debuffRange)
        {
            Initialize();

            if (_cells == null) return;

            // 1. 전체 초기화 (필드 스프라이트)
            foreach (var cell in _cells)
            {
                if (cell != null) cell.SetSprite(settings.fieldSprite);
            }

            // 2. 효과 범위 표시
            if (effectRange != null)
            {
                foreach (var offset in effectRange)
                {
                    Vector2Int pos = new Vector2Int(_center.x + offset.x, _center.y - offset.y);
                    if (IsValidPos(pos) && _cells[pos.x, pos.y] != null)
                    {
                        _cells[pos.x, pos.y].SetSprite(settings.effectSprite);
                        Debug.Log($"[TotemUI] Effect Range at {pos} (offset: {offset})");
                    }
                }
            }

            // 3. 디버프 범위 표시
            if (debuffRange != null)
            {
                foreach (var offset in debuffRange)
                {
                    Vector2Int pos = new Vector2Int(_center.x + offset.x, _center.y - offset.y);
                    if (IsValidPos(pos) && _cells[pos.x, pos.y] != null)
                    {
                        _cells[pos.x, pos.y].SetSprite(settings.debuffSprite);
                        Debug.Log($"[TotemUI] Debuff Range at {pos} (offset: {offset})");
                    }
                }
            }

            // 4. 토템 위치 표시 (중앙)
            if (IsValidPos(_center) && _cells[_center.x, _center.y] != null)
            {
                _cells[_center.x, _center.y].SetSprite(settings.totemSprite);
            }
            else
            {
                Debug.LogWarning($"[TotemUI] Totem position out of bounds: {_center}");
            }
        }

        private bool IsValidPos(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y;
        }
    }
}
