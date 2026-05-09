using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI
{
    /// <summary>
    /// GridLayoutGroup의 CellSize를 부모의 너비에 맞춰 동적으로 계산하여 
    /// 한 줄에 고정된 개수(columnCount)를 유지하게 해주는 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public class DynamicGridLayout : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int columnCount = 5;
        [SerializeField] private bool maintainSquareRatio = true;
        [SerializeField] private float aspectRatio = 1.0f;

        private GridLayoutGroup grid;
        private RectTransform rectTransform;

        private void Awake()
        {
            grid = GetComponent<GridLayoutGroup>();
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            UpdateCellSize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터에서 값이 변경될 때마다 셀 크기 업데이트
            UpdateCellSize();
        }
#endif

        public void UpdateCellSize()
        {
            if (grid == null) grid = GetComponent<GridLayoutGroup>();
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

            if (columnCount <= 0) columnCount = 1;

            // 부모(본인)의 너비 확인
            float parentWidth = rectTransform.rect.width;

            // 패딩 제외
            float paddingX = grid.padding.left + grid.padding.right;
            
            // 간격 제외 (columnCount - 1 만큼의 간격이 존재)
            float spacingX = grid.spacing.x * (columnCount - 1);

            // 최종 셀 너비 계산
            float cellWidth = (parentWidth - paddingX - spacingX) / columnCount;

            if (cellWidth < 0) cellWidth = 0;

            // 높이 계산 (정사각형 유지 혹은 비율 적용)
            float cellHeight = maintainSquareRatio ? cellWidth : cellWidth * aspectRatio;

            grid.cellSize = new Vector2(cellWidth, cellHeight);
        }
    }
}