using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace GaeGGUL.UI.Totem
{
    /// <summary>
    /// 토템의 효과 범위를 시각적으로 보여주는 그리드 컴포넌트입니다.
    /// 범례(Legend)를 슬롯 기반으로 관리합니다.
    /// 그리드 크기는 SetRange/SetData에 전달된 범위의 바운딩 박스에 맞춰 매번 자동으로 재계산됩니다
    /// (컨테이너의 픽셀 크기는 그대로 두고, 칸 크기는 UIGridLayout이 그리드 크기에 맞춰 자동 조절함).
    /// </summary>
    public class UI_TotemRangeGrid : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private TotemDisplaySettings settings;
        [Tooltip("데이터가 아직 설정되지 않았을 때(에디터 미리보기 등) 사용하는 기본 그리드 크기.")]
        [SerializeField] private Vector2Int gridSize = new Vector2Int(6, 4);
        [Tooltip("데이터가 아직 설정되지 않았을 때 사용하는 기본 토템 위치 (1-based, 좌상단 기준).")]
        [SerializeField] private Vector2Int totemPosition = new Vector2Int(4, 2);
        [SerializeField] private UI_TotemRangeCell cellPrefab;
        [SerializeField] private UIGridLayout gridLayout;

        [Header("Legend Settings")]
        [SerializeField] private UI_TotemLegendSlot legendSlotPrefab;
        [SerializeField] private Transform tr_LegendParent;

        private UI_TotemRangeCell[,] _cells;
        private Vector2Int _center;
        private Vector2Int _currentGridSize;
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

        /// <summary>기본(고정) 크기로 그리드를 준비합니다. 실제 토템 데이터가 들어오기 전 미리보기용입니다.</summary>
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

            DisableConflictingLayoutGroup();
            SetupLegend();

            var center = new Vector2Int(totemPosition.x - 1, gridSize.y - totemPosition.y);
            BuildGrid(gridSize, center);

            _isInitialized = true;
        }

        private void DisableConflictingLayoutGroup()
        {
            // GridLayoutGroup이 있으면 수동 레이아웃을 위해 비활성화
            if (gridLayout == null) return;
            var glg = gridLayout.GetComponent<GridLayoutGroup>();
            if (glg != null && glg.enabled) glg.enabled = false;
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
                _currentGridSize = gridSize;
                _center = new Vector2Int(totemPosition.x - 1, gridSize.y - totemPosition.y);
                _isInitialized = true;
            }
        }

        public void RefreshLayoutPositions()
        {
            if (gridLayout == null || _cells == null) return;
            if (_cells.GetLength(0) != _currentGridSize.x || _cells.GetLength(1) != _currentGridSize.y) return;

            for (int y = 0; y < _currentGridSize.y; y++)
            {
                for (int x = 0; x < _currentGridSize.x; x++)
                {
                    var cell = _cells[x, y];
                    if (cell == null) continue;

                    var cellRect = cell.transform as RectTransform;
                    var (pos, size) = gridLayout.GetCellRect(_currentGridSize, new Vector2Int(x, y));
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
                (settings.effectLabel, settings.effectSprite)
            };

            foreach (var data in legendData)
            {
                var slot = Instantiate(legendSlotPrefab, tr_LegendParent);
                slot.SetData(data.label, data.sprite);
            }
        }

        /// <summary>지정한 크기·중심으로 그리드 셀을 새로 생성합니다. 기존 셀은 전부 정리됩니다.</summary>
        private void BuildGrid(Vector2Int size, Vector2Int center)
        {
            if (cellPrefab == null || gridLayout == null) return;

            // 기존 그리드 자식 정리
            for (int i = gridLayout.transform.childCount - 1; i >= 0; i--)
            {
                var child = gridLayout.transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            _cells = new UI_TotemRangeCell[size.x, size.y];
            _currentGridSize = size;
            _center = center;

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var cell = Instantiate(cellPrefab, gridLayout.transform);
                    var cellRect = cell.transform as RectTransform;

                    var (pos, cellSize) = gridLayout.GetCellRect(size, new Vector2Int(x, y));
                    gridLayout.ApplyLayoutToChild(cellRect, pos, cellSize);

                    _cells[x, y] = cell;
                    _cells[x, y].SetSprite(settings.fieldSprite); // 초기화 시 필드 스프라이트
                }
            }
        }

        /// <summary>표시할 오프셋들(+ 토템 자신의 칸)을 전부 담는 최소 크기와, 그 안에서의 토템 중심 좌표를 계산합니다.</summary>
        private (Vector2Int size, Vector2Int center) ComputeAutoBounds(List<Vector2Int> effectRange, List<Vector2Int> debuffRange)
        {
            int minX = 0, maxX = 0, minY = 0, maxY = 0; // 토템 자신의 칸(0,0)은 항상 포함

            void Consider(List<Vector2Int> offsets)
            {
                if (offsets == null) return;
                foreach (var o in offsets)
                {
                    if (o.x < minX) minX = o.x;
                    if (o.x > maxX) maxX = o.x;
                    if (o.y < minY) minY = o.y;
                    if (o.y > maxY) maxY = o.y;
                }
            }
            Consider(effectRange);
            Consider(debuffRange);

            var size = new Vector2Int(maxX - minX + 1, maxY - minY + 1);
            var center = new Vector2Int(-minX, maxY);
            return (size, center);
        }

        public void SetRange(List<Vector2Int> effectRange, List<Vector2Int> debuffRange)
        {
            bool hasRange = (effectRange != null && effectRange.Count > 0) || (debuffRange != null && debuffRange.Count > 0);
            if (!hasRange)
            {
                // Only hide the actual grid cell container (gridLayout, e.g. Grid_List — a sibling
                // of this component's own GameObject, not a child of it). This component's own
                // GameObject (Totem_Grid_Stat_Area) also parents the unrelated stat/description text
                // (Stat_Area/Stat_Text), so it must stay active or the description disappears too.
                if (gridLayout != null) gridLayout.gameObject.SetActive(false);
                return;
            }
            if (gridLayout != null) gridLayout.gameObject.SetActive(true);

            if (settings == null)
            {
                Debug.LogError("[TotemUI] TotemDisplaySettings is missing.");
                return;
            }

            DisableConflictingLayoutGroup();
            if (!_isInitialized)
            {
                SetupLegend();
                _isInitialized = true;
            }

            var (size, center) = ComputeAutoBounds(effectRange, debuffRange);
            BuildGrid(size, center);

            if (_cells == null) return;

            // 효과 범위 표시
            if (effectRange != null)
            {
                foreach (var offset in effectRange)
                {
                    Vector2Int pos = new Vector2Int(_center.x + offset.x, _center.y - offset.y);
                    if (IsValidPos(pos) && _cells[pos.x, pos.y] != null)
                        _cells[pos.x, pos.y].SetSprite(settings.effectSprite);
                }
            }

            // 디버프 범위 표시
            if (debuffRange != null)
            {
                foreach (var offset in debuffRange)
                {
                    Vector2Int pos = new Vector2Int(_center.x + offset.x, _center.y - offset.y);
                    if (IsValidPos(pos) && _cells[pos.x, pos.y] != null)
                        _cells[pos.x, pos.y].SetSprite(settings.debuffSprite);
                }
            }

            // 토템 위치 표시 (중앙) — 바운딩 박스에 항상 포함되므로 유효함이 보장됨
            if (IsValidPos(_center) && _cells[_center.x, _center.y] != null)
            {
                _cells[_center.x, _center.y].SetSprite(settings.totemSprite);
            }
        }

        /// <summary>Shows effect-specific colors from the same SO used by gameplay.</summary>
        public void SetData(TotemData data)
        {
            SetRange(data != null ? data.GetEffectPreviewOffsets() : null, null);
            if (_cells == null) return;
            if (data == null || !data.HasEffectGroups) { SetupLegend(); return; }
            foreach (var group in data.EffectGroups)
            {
                if (group == null) continue;
                foreach (var offset in group.GetPreviewOffsets())
                {
                    var pos = new Vector2Int(_center.x + offset.x, _center.y - offset.y);
                    if (pos != _center && IsValidPos(pos) && _cells[pos.x, pos.y] != null)
                    {
                        _cells[pos.x, pos.y].SetSprite(settings.fieldSprite);
                        _cells[pos.x, pos.y].SetColor(group.Color);
                    }
                }
            }
            if (legendSlotPrefab == null || tr_LegendParent == null) return;
            for (int i = tr_LegendParent.childCount - 1; i >= 0; i--)
            {
                var child = tr_LegendParent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            foreach (var group in data.EffectGroups)
            {
                if (group == null) continue;
                var slot = Instantiate(legendSlotPrefab, tr_LegendParent);
                slot.SetData(string.IsNullOrWhiteSpace(group.Label) ? "효과" : group.Label, settings.fieldSprite);
                slot.SetColor(group.Color);
            }
        }

        private bool IsValidPos(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < _currentGridSize.x && pos.y >= 0 && pos.y < _currentGridSize.y;
        }
    }
}
