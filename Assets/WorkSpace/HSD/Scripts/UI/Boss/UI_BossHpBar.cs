using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_BossHpBar : MonoBehaviour
{
    [Header("Bar")]
    [Tooltip("HP에 따라 가로 길이가 줄어드는 Mask 영역")]
    [SerializeField] private RectTransform _hpFillMask;

    [Tooltip("실제 HP 색상을 담당")]
    [SerializeField] private Image _hpColor;

    [Tooltip("Tiled 방식의 스테인드글라스 패턴")]
    [SerializeField] private Image _patternOverlay;


    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI _hpPercentText;
    [SerializeField] private TextMeshProUGUI _hpLineText;


    [Header("HP Line Colors (1줄 → N줄)")]
    [Tooltip("배열 길이 = 총 체력줄 수. index 0 = 1줄(마지막), 마지막 index = 최고줄")]
    [SerializeField] private Color[] _lineColors =
    {
        new Color(0.30f, 0.85f, 0.35f), // 1줄 Green
        new Color(0.95f, 0.85f, 0.25f), // 2줄 Yellow
        new Color(0.95f, 0.55f, 0.15f), // 3줄 Orange
        new Color(0.90f, 0.25f, 0.25f), // 4줄 Red
        new Color(0.65f, 0.30f, 0.85f), // 5줄 Purple
    };


    [Header("Pattern")]
    [SerializeField] private Color _patternColor = Color.white;

    [Range(0f, 1f)]
    [SerializeField] private float _patternAlpha = 0.35f;


    [Header("Text Format")]
    [SerializeField] private string _percentFormat = "0.00";
    [SerializeField] private string _lineSuffix = "줄";


    public int LineCount => _lineColors != null ? _lineColors.Length : 0;


    private void Awake() => ApplyPatternColor();


    public void SetHp(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            maxHp = 1f;

        float ratio = Mathf.Clamp01(currentHp / maxHp);


        // 전체 체력 비율만큼 Mask 가로 길이 설정 (왼쪽 고정, 오른쪽이 줄어듦).
        // 앵커 기반이라 Pivot 설정과 무관하게 항상 왼쪽 정렬로 채워지고,
        // 부모 폭에 상대적이라 에디터/플레이 모두 정확히 동작한다.
        if (_hpFillMask != null)
        {
            Vector2 anchorMin = _hpFillMask.anchorMin;
            Vector2 anchorMax = _hpFillMask.anchorMax;
            anchorMin.x = 0f;
            anchorMax.x = ratio;
            _hpFillMask.anchorMin = anchorMin;
            _hpFillMask.anchorMax = anchorMax;

            // 가로 오프셋 0 → 앵커 사이를 정확히 채움 (세로는 기존 설정 유지)
            Vector2 offsetMin = _hpFillMask.offsetMin;
            Vector2 offsetMax = _hpFillMask.offsetMax;
            offsetMin.x = 0f;
            offsetMax.x = 0f;
            _hpFillMask.offsetMin = offsetMin;
            _hpFillMask.offsetMax = offsetMax;
        }


        int lineCount = LineCount;

        int currentLine =
            ratio <= 0f || lineCount <= 0
                ? 0
                : Mathf.Clamp(
                    Mathf.CeilToInt(ratio * lineCount),
                    1,
                    lineCount
                );


        // 현재 구간 색상만 적용 (배열 index = 줄 - 1)
        if (_hpColor != null && currentLine >= 1)
            _hpColor.color = _lineColors[currentLine - 1];


        ApplyPatternColor();


        if (_hpPercentText != null)
        {
            _hpPercentText.text =
                (ratio * 100f).ToString(_percentFormat) + "%";
        }


        if (_hpLineText != null)
            _hpLineText.text = currentLine + _lineSuffix;
    }


    private void ApplyPatternColor()
    {
        if (_patternOverlay == null)
            return;

        Color c = _patternColor;
        c.a = _patternAlpha;

        _patternOverlay.color = c;
    }


#if UNITY_EDITOR

    private void OnValidate() => ApplyPatternColor();

#endif
}