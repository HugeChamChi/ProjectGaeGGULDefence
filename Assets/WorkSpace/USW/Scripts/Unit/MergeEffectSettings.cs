using UnityEngine;

/// <summary>
/// 합성 연출 조정값. Resources/MergeEffectSettings 에서 로드되어 인게임 스코프에 등록된다.
/// 순서: 재료 잔상 흡입 → 먼지구름 펑 → 소환 줄기가 솟았다 합성 칸에 내리꽂힘 → 유닛 등장.
/// </summary>
[CreateAssetMenu(fileName = "MergeEffectSettings", menuName = "USW/Merge Effect Settings")]
public class MergeEffectSettings : ScriptableObject
{
    [Header("재료 흡입")]
    [Tooltip("재료 유닛 잔상이 합성 칸으로 빨려드는 시간(게임 시간, 초)")]
    [SerializeField, Min(0f)] private float _gatherSeconds = 0.2f;
    [Tooltip("흡입이 끝날 때 잔상 크기 배율")]
    [SerializeField, Range(0f, 1f)] private float _gatherEndScale = 0.35f;

    [Header("먼지구름")]
    [SerializeField] private Sprite[] _dustSprites;
    [SerializeField] private Sprite _tickSprite;
    [SerializeField] private string _sortingLayerName = "FX";
    [Tooltip("FX 레이어 정렬 순서. 소환 줄기(Line, FX 0)보다 낮아야 줄기가 먼지 위로 보인다")]
    [SerializeField] private int _sortingOrder = -100;
    [Tooltip("중앙 큰 구름 + 바깥으로 퍼지는 작은 구름 개수")]
    [SerializeField, Range(0, 8)] private int _puffCount = 4;
    [Tooltip("중앙 구름 월드 크기")]
    [SerializeField, Min(0f)] private float _centerPuffSize = 1.1f;
    [Tooltip("바깥 구름 월드 크기")]
    [SerializeField, Min(0f)] private float _sidePuffSize = 0.6f;
    [Tooltip("바깥 구름이 퍼지는 거리(월드)")]
    [SerializeField, Min(0f)] private float _puffSpread = 0.55f;
    [Tooltip("먼지구름 전체 지속 시간(게임 시간, 초)")]
    [SerializeField, Min(0.01f)] private float _puffSeconds = 0.45f;
    [Tooltip("충격선 개수")]
    [SerializeField, Range(0, 8)] private int _tickCount = 4;
    [Tooltip("충격선 월드 길이")]
    [SerializeField, Min(0f)] private float _tickSize = 0.3f;
    [Tooltip("충격선이 튀어나가는 거리(월드)")]
    [SerializeField, Min(0f)] private float _tickDistance = 0.75f;

    [Header("소환 줄기")]
    [Tooltip("먼지구름이 터진 뒤 줄기가 출발하기까지 대기(게임 시간, 초). 먼지가 걷히기 시작할 때 소환 연출이 보이도록")]
    [SerializeField, Min(0f)] private float _lineDelaySeconds = 0.3f;
    [Tooltip("줄기 출발점: 합성 칸 기준 오프셋(월드). (0,0)이면 제자리에서 수직으로 솟았다 내려꽂힌다. 가로 값은 화면 중앙 쪽으로 자동 반전")]
    [SerializeField] private Vector2 _lineStartOffset = Vector2.zero;
    [Tooltip("줄기 포물선 높이(월드)")]
    [SerializeField, Min(0f)] private float _lineCurveHeight = 3f;

    public float GatherSeconds => _gatherSeconds;
    public float GatherEndScale => _gatherEndScale;
    public Sprite[] DustSprites => _dustSprites;
    public Sprite TickSprite => _tickSprite;
    public string SortingLayerName => _sortingLayerName;
    public int SortingOrder => _sortingOrder;
    public int PuffCount => _puffCount;
    public float CenterPuffSize => _centerPuffSize;
    public float SidePuffSize => _sidePuffSize;
    public float PuffSpread => _puffSpread;
    public float PuffSeconds => _puffSeconds;
    public int TickCount => _tickCount;
    public float TickSize => _tickSize;
    public float TickDistance => _tickDistance;
    public float LineDelaySeconds => _lineDelaySeconds;
    public Vector2 LineStartOffset => _lineStartOffset;
    public float LineCurveHeight => _lineCurveHeight;
}
