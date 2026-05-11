using UnityEngine;

namespace GaeGGUL.UI
{
    /// <summary>
    /// RectTransform의 자식들을 그리드 형태로 배치하는 수동 레이아웃 컴포넌트입니다.
    /// GridLayoutGroup의 제약을 벗어나 세밀한 제어가 가능합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIGridLayout : MonoBehaviour
    {
        [Header("Padding")]
        public float paddingLeft;
        public float paddingRight;
        public float paddingTop;
        public float paddingBottom;

        [Header("Spacing")]
        public Vector2 spacing;

        [Header("Options")]
        public bool preserveSquare = true; // 셀을 항상 정사각형으로 유지할지 여부

        public event System.Action OnLayoutChanged;

        private RectTransform _rectTransform;
        public RectTransform Rect => _rectTransform ??= GetComponent<RectTransform>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            OnLayoutChanged?.Invoke();
        }
#endif

        private void OnRectTransformDimensionsChange()
        {
            OnLayoutChanged?.Invoke();
        }

        /// <summary>
        /// 특정 그리드 좌표에 대한 Rect 정보를 계산합니다.
        /// </summary>
        public (Vector2 position, Vector2 size) GetCellRect(Vector2Int gridSize, Vector2Int coord)
        {
            Rect rect = Rect.rect;
            
            // 사용 가능한 총 너비/높이 계산
            float availableWidth = rect.width - paddingLeft - paddingRight - (spacing.x * (gridSize.x - 1));
            float availableHeight = rect.height - paddingTop - paddingBottom - (spacing.y * (gridSize.y - 1));

            // 기본 셀 사이즈 계산
            float cellWidth = availableWidth / gridSize.x;
            float cellHeight = availableHeight / gridSize.y;
            
            float finalCellWidth = cellWidth;
            float finalCellHeight = cellHeight;

            // 정사각형 유지 로직
            if (preserveSquare)
            {
                float size = Mathf.Min(cellWidth, cellHeight);
                finalCellWidth = size;
                finalCellHeight = size;
            }

            Vector2 cellSize = new Vector2(finalCellWidth, finalCellHeight);

            // 전체 그리드가 차지하는 영역 계산 (정렬용)
            float totalGridWidth = (finalCellWidth * gridSize.x) + (spacing.x * (gridSize.x - 1));
            float totalGridHeight = (finalCellHeight * gridSize.y) + (spacing.y * (gridSize.y - 1));

            // 중앙 정렬을 위한 시작 오프셋 계산
            float offsetX = (rect.width - paddingLeft - paddingRight - totalGridWidth) / 2f;
            float offsetY = (rect.height - paddingTop - paddingBottom - totalGridHeight) / 2f;

            float posX = paddingLeft + offsetX + (coord.x * (finalCellWidth + spacing.x));
            float posY = paddingBottom + offsetY + (coord.y * (finalCellHeight + spacing.y));
            Vector2 position = new Vector2(posX, posY);

            return (position, cellSize);
        }

        /// <summary>
        /// 자식 오브젝트(RectTransform)의 앵커와 피벗을 그리드 배치에 최적화된 상태로 초기화합니다.
        /// </summary>
        public void ApplyLayoutToChild(RectTransform childRect, Vector2 position, Vector2 size)
        {
            if (childRect == null) return;

            childRect.anchorMin = Vector2.zero;
            childRect.anchorMax = Vector2.zero;
            childRect.pivot = Vector2.zero;
            
            childRect.anchoredPosition = position;
            childRect.sizeDelta = size;
        }
    }
}
