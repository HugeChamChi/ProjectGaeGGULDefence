using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 아트 검수 씬 전용 — 실제 GridCell_Prefab을 4×6으로 스폰하고
/// Slider로 전체 투명도를 조절합니다.
///
/// 씬 연결 방법:
///   1. PreviewContainer 안에 빈 GameObject "GridOverlay" 추가 (Anchor: stretch fill)
///      → RectTransform을 PreviewContainer에 꽉 채우기
///      → CanvasGroup 컴포넌트 추가
///      → GridLayoutGroup 컴포넌트 추가
///   2. 이 스크립트를 GridOverlay에 부착
///   3. Inspector 연결:
///      _cellPrefab    → Assets/WorkSpace/USW/Prefab/etc/GridCell_Prefab
///      _gridLayout    → GridOverlay의 GridLayoutGroup
///      _canvasGroup   → GridOverlay의 CanvasGroup
///      _opacitySlider → ControlPanel의 Slider
///      _opacityLabel  → Slider 옆 TMP Text
/// </summary>
public class GridOverlayController : MonoBehaviour
{
    [Header("셀 프리팹")]
    [SerializeField] private GameObject _cellPrefab;

    [Header("그리드 컨테이너")]
    [SerializeField] private GridLayoutGroup _gridLayout;
    [SerializeField] private CanvasGroup     _canvasGroup;

    [Header("그리드 설정")]
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows    = 6;

    [Header("셀 크기 (0이면 컨테이너 크기에서 자동 계산)")]
    [SerializeField] private Vector2 _cellSizeOverride = Vector2.zero;

    [Header("슬라이더 UI")]
    [SerializeField] private Slider          _opacitySlider;
    [SerializeField] private TextMeshProUGUI _opacityLabel;

    [Header("기본 투명도")]
    [SerializeField] [Range(0f, 1f)] private float _defaultOpacity = 0.4f;

    private void Start()
    {
        // 레이아웃 크기가 확정된 후 계산하기 위해 강제 갱신
        Canvas.ForceUpdateCanvases();

        SetupGridLayout();
        SpawnCells();

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;

        _opacitySlider.minValue = 0f;
        _opacitySlider.maxValue = 1f;
        _opacitySlider.value    = _defaultOpacity;
        _opacitySlider.onValueChanged.AddListener(SetOpacity);
        SetOpacity(_defaultOpacity);
    }

    private void SetupGridLayout()
    {
        var rt = _gridLayout.GetComponent<RectTransform>();

        Vector2 cellSize = _cellSizeOverride;
        if (cellSize.x <= 0f || cellSize.y <= 0f)
        {
            // 컨테이너 크기를 열/행 수로 나눠 자동 계산
            cellSize = new Vector2(
                rt.rect.width  / _columns,
                rt.rect.height / _rows
            );
        }

        _gridLayout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        _gridLayout.constraintCount = _columns;
        _gridLayout.cellSize        = cellSize;
        _gridLayout.spacing         = Vector2.zero;
        _gridLayout.padding         = new RectOffset(0, 0, 0, 0);
    }

    private void SpawnCells()
    {
        // 기존 자식 정리
        foreach (Transform child in _gridLayout.transform)
            Destroy(child.gameObject);

        for (int i = 0; i < _columns * _rows; i++)
        {
            var cell = Instantiate(_cellPrefab, _gridLayout.transform);
            cell.name = $"PreviewCell_{i}";

            // 게임 로직 컴포넌트는 비활성화 (시각만 남김)
            var gridCell = cell.GetComponent<GridCell>();
            if (gridCell != null) gridCell.enabled = false;
        }
    }

    private void SetOpacity(float value)
    {
        _canvasGroup.alpha = value;
        if (_opacityLabel != null)
            _opacityLabel.text = $"그리드: {value * 100f:F0}%";
    }
}
