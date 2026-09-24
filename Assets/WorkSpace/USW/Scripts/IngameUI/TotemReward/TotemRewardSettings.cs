using GaeGGUL.UI.Totem;
using TMPro;
using UnityEngine;

/// <summary>
/// 보스 처치 토템 보상 화면(TotemRewardUI) 조정값.
/// 흐름: 화면 어두워짐 → 사선 띠 3개 등장(선택지 개요) → 띠 터치 → 상세(설명·범위·확정) → 확정 시 획득.
/// </summary>
[CreateAssetMenu(fileName = "TotemRewardSettings", menuName = "USW/Totem Reward Settings")]
public class TotemRewardSettings : ScriptableObject
{
    [Header("표시 방식 (테스트 씬 토글의 시작값)")]
    [Tooltip("개요(사선 띠)에 토템 설명을 함께 보여준다.")]
    [SerializeField] private bool _showDescriptionOnOverview = true;
    [Tooltip("상세 화면에서 좌우로 밀어 다른 선택지 상세로 넘긴다.")]
    [SerializeField] private bool _allowDetailSwipe = true;
    [Tooltip("띠 색: 켜면 토템 등급 색, 끄면 자리 고정 색.")]
    [SerializeField] private bool _useTierColors = false;

    [Header("색")]
    [Tooltip("자리 고정 색 (위 → 아래).")]
    [SerializeField] private Color[] _slotColors =
    {
        new Color(0.16f, 0.84f, 0.96f, 1f),
        new Color(0.95f, 0.30f, 0.17f, 1f),
        new Color(0.96f, 0.74f, 0.17f, 1f),
    };
    [Tooltip("등급 색 출처. 각 등급의 textColor를 띠 색으로 쓴다.")]
    [SerializeField] private UnitTierPalette _tierPalette;
    [SerializeField] private Color _edgeColor = Color.black;
    [SerializeField, Min(0f)] private float _edgeWidth = 16f;
    [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.7f;

    [Header("띠 경계 (화면 위에서부터 비율, 왼쪽/오른쪽)")]
    [SerializeField] private Vector2 _upperBoundary = new Vector2(0.38f, 0.22f);
    [SerializeField] private Vector2 _lowerBoundary = new Vector2(0.60f, 0.78f);

    [Header("글꼴 · 글자")]
    [SerializeField] private TMP_FontAsset _font;
    [Tooltip("상세 화면 뒤로가기 아이콘 (흰색으로 표시). 비우면 기본 삼각형.")]
    [SerializeField] private Sprite _backIcon;
    [SerializeField, Min(10f)] private float _overviewNameSize = 72f;
    [SerializeField, Min(10f)] private float _overviewDescriptionSize = 42f;
    [SerializeField, Min(10f)] private float _detailNameSize = 72f;
    [SerializeField, Min(10f)] private float _detailDescriptionSize = 44f;
    [SerializeField] private string _confirmLabel = "확정";
    [SerializeField] private string _swipeHint = "좌우로 넘겨 비교";

    [Header("크기 (기준 해상도 1080x1920)")]
    [SerializeField, Min(50f)] private float _overviewIconSize = 380f;
    [SerializeField, Min(50f)] private float _detailIconSize = 360f;

    [Header("범위 그리드 (토템 정보창과 동일한 모양)")]
    [SerializeField] private TotemDisplaySettings _rangeDisplaySettings;
    [SerializeField] private UI_TotemRangeCell _rangeCellPrefab;
    [Tooltip("정보창 Grid_List 박스 스프라이트 (SPR_UI2400_RangeGridBg).")]
    [SerializeField] private Sprite _rangeBoxSprite;
    [Tooltip("박스 크기 — 정보창 Grid_List와 같은 비율.")]
    [SerializeField] private Vector2 _rangeBoxSize = new Vector2(233f, 284f);
    [Tooltip("전체 화면 상세에서 읽히도록 박스 전체를 키우는 배율.")]
    [SerializeField, Min(0.5f)] private float _rangeBoxScale = 1.6f;

    [Header("연출 시간 (실제 초, 게임 정지 중에도 재생)")]
    [SerializeField, Min(0f)] private float _dimSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float _bandSlideSeconds = 0.3f;
    [SerializeField, Min(0f)] private float _bandStaggerSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float _switchSeconds = 0.2f;
    [Tooltip("상세 화면에서 이 거리(기준 해상도 px) 이상 밀면 다음/이전 선택지로 넘긴다.")]
    [SerializeField, Min(10f)] private float _swipeThreshold = 120f;

    public bool ShowDescriptionOnOverview => _showDescriptionOnOverview;
    public bool AllowDetailSwipe => _allowDetailSwipe;
    public bool UseTierColors => _useTierColors;
    public Color EdgeColor => _edgeColor;
    public float EdgeWidth => _edgeWidth;
    public float DimAlpha => _dimAlpha;
    public Vector2 UpperBoundary => _upperBoundary;
    public Vector2 LowerBoundary => _lowerBoundary;
    public TMP_FontAsset Font => _font;
    public Sprite BackIcon => _backIcon;
    public float OverviewNameSize => _overviewNameSize;
    public float OverviewDescriptionSize => _overviewDescriptionSize;
    public float DetailNameSize => _detailNameSize;
    public float DetailDescriptionSize => _detailDescriptionSize;
    public string ConfirmLabel => _confirmLabel;
    public string SwipeHint => _swipeHint;
    public float OverviewIconSize => _overviewIconSize;
    public float DetailIconSize => _detailIconSize;
    public TotemDisplaySettings RangeDisplaySettings => _rangeDisplaySettings;
    public UI_TotemRangeCell RangeCellPrefab => _rangeCellPrefab;
    public Sprite RangeBoxSprite => _rangeBoxSprite;
    public Vector2 RangeBoxSize => _rangeBoxSize;
    public float RangeBoxScale => _rangeBoxScale;
    public float DimSeconds => _dimSeconds;
    public float BandSlideSeconds => _bandSlideSeconds;
    public float BandStaggerSeconds => _bandStaggerSeconds;
    public float SwitchSeconds => _switchSeconds;
    public float SwipeThreshold => _swipeThreshold;

    /// <summary>띠 색. 등급 색 모드면 팔레트의 등급 글자색, 아니면 자리 고정 색.</summary>
    public Color GetBandColor(int slot, Tier tier, bool useTierColors)
    {
        if (useTierColors && _tierPalette != null)
        {
            var info = _tierPalette.GetInfo(tier);
            if (info != null) return info.textColor;
        }
        if (_slotColors == null || _slotColors.Length == 0) return Color.white;
        return _slotColors[Mathf.Clamp(slot, 0, _slotColors.Length - 1)];
    }
}
