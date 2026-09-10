using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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


    [Header("HP Lines")]
    [Min(1)]
    [Tooltip("총 체력줄 수 (예: 100). 색은 아래 Line Colors 를 순서대로 분배·보간")]
    [SerializeField] private int _lineCount = 5;

    /// <summary>줄 색 계산 방식.</summary>
    public enum LineColorMode
    {
        Distribute,        // 팔레트를 전체 줄에 부드럽게 보간 분배
        DistributeStepped, // 전체 줄에 구간(band)으로 스냅
        CyclePerLine,      // 한 줄마다 팔레트 다음 색으로 순환 (풀피 = 마지막 색)
    }

    [Tooltip("색 방식: Distribute(보간) / DistributeStepped(구간) / CyclePerLine(줄마다 순환)")]
    [SerializeField] private LineColorMode _colorMode = LineColorMode.CyclePerLine;

    [Tooltip("줄 색상 팔레트. 줄 수에 순서대로 분배됨. index 0 = 1줄쪽, 마지막 index = 최고줄쪽")]
    [SerializeField] private Color[] _lineColors =
    {
        new Color(0.30f, 0.85f, 0.35f), // Green
        new Color(0.95f, 0.85f, 0.25f), // Yellow
        new Color(0.95f, 0.55f, 0.15f), // Orange
        new Color(0.90f, 0.25f, 0.25f), // Red
        new Color(0.65f, 0.30f, 0.85f), // Purple
    };


    [Header("Pattern")]
    [SerializeField] private Color _patternColor = Color.white;

    [Range(0f, 1f)]
    [SerializeField] private float _patternAlpha = 0.35f;


    [Header("Text Format")]
    [SerializeField] private string _percentFormat = "0.00";
    [SerializeField] private string _lineSuffix = "줄";


    [Header("Damage Tween (DOTween)")]
    [Tooltip("피해 시 바/퍼센트를 부드럽게 보간")]
    [SerializeField] private bool _smooth = true;
    [SerializeField] private float _tweenDuration = 0.3f;

    private float _shownRatio;
    private bool _initialized;
    private Tween _barTween;


    public int LineCount => Mathf.Max(1, _lineCount);

    /// <summary>현재 남은 체력줄 (0 = 사망).</summary>
    public int CurrentLine => _currentLine;
    private int _currentLine = -1;

    /// <summary>현재 줄이 변경될 때 새 줄 번호와 함께 발생 (흔들림 연출 등 구독용).</summary>
    public event Action<int> OnCurrentLineChanged;


    private void Awake() => ApplyPatternColor();

    private void OnDestroy() => _barTween?.Kill();

    /// <summary>런타임에서 총 체력줄 수 변경 (디버그/설정용). 이후 SetHp 호출 시 즉시 반영.</summary>
    public void SetLineCount(int count)
    {
        _lineCount = Mathf.Max(1, count);
        ApplyBarVisual(_shownRatio);
    }


    public void SetHp(float currentHp, float maxHp)
    {
        if (maxHp <= 0f)
            maxHp = 1f;

        float ratio = Mathf.Clamp01(currentHp / maxHp);
        int lineCount = LineCount;

        // 이벤트/줄 판정은 목표값 기준으로 즉시 (흔들림·유리깨짐이 바로 터지게)
        int targetLine =
            ratio <= 0f
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(ratio * lineCount), 1, lineCount);

        if (targetLine != _currentLine)
        {
            _currentLine = targetLine;
            OnCurrentLineChanged?.Invoke(targetLine);
        }

        // 바/퍼센트는 부드럽게 보간 (첫 세팅·에디터·비활성 시엔 즉시)
        _barTween?.Kill();

        if (_smooth && _initialized && Application.isPlaying)
        {
            _barTween = DOTween.To(
                    () => _shownRatio,
                    v => { _shownRatio = v; ApplyBarVisual(v); },
                    ratio,
                    _tweenDuration)
                .SetUpdate(true)
                .SetEase(Ease.OutQuad);
        }
        else
        {
            _shownRatio = ratio;
            ApplyBarVisual(ratio);
        }

        _initialized = true;
    }

    /// <summary>보간된 표시 비율(shownRatio) 기준으로 바 길이·색·텍스트를 갱신.</summary>
    private void ApplyBarVisual(float shownRatio)
    {
        int lineCount = LineCount;

        // 전체 비율만큼 Mask 가로 길이 (왼쪽 고정, 오른쪽이 줄어듦).
        // 앵커 기반이라 Pivot 설정과 무관하게 왼쪽 정렬 + 부모 폭 상대.
        if (_hpFillMask != null)
        {
            Vector2 anchorMin = _hpFillMask.anchorMin;
            Vector2 anchorMax = _hpFillMask.anchorMax;
            anchorMin.x = 0f;
            anchorMax.x = shownRatio;
            _hpFillMask.anchorMin = anchorMin;
            _hpFillMask.anchorMax = anchorMax;

            Vector2 offsetMin = _hpFillMask.offsetMin;
            Vector2 offsetMax = _hpFillMask.offsetMax;
            offsetMin.x = 0f;
            offsetMax.x = 0f;
            _hpFillMask.offsetMin = offsetMin;
            _hpFillMask.offsetMax = offsetMax;
        }

        int shownLine =
            shownRatio <= 0f
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(shownRatio * lineCount), 1, lineCount);

        if (_hpColor != null && shownLine >= 1)
            _hpColor.color = EvaluateLineColor(shownLine, lineCount);

        ApplyPatternColor();

        if (_hpPercentText != null)
            _hpPercentText.text = (shownRatio * 100f).ToString(_percentFormat) + "%";

        if (_hpLineText != null)
            _hpLineText.text = shownLine + _lineSuffix;
    }


    /// <summary>
    /// 현재 줄(1~lineCount)을 팔레트 색에 순서대로 분배하고 사이는 보간해서 반환한다.
    /// 1줄 → 첫 색, 최고줄 → 마지막 색.
    /// </summary>
    private Color EvaluateLineColor(int currentLine, int lineCount)
    {
        if (_lineColors == null || _lineColors.Length == 0)
            return Color.white;

        int k = _lineColors.Length;
        if (k == 1)
            return _lineColors[0];

        // 줄마다 순환: 풀피 = 마지막 색, 1줄 깎일 때마다 팔레트 앞으로 순환
        // 예) 200줄=보라(마지막), 199줄=green(0), 198줄=yellow(1) ...
        if (_colorMode == LineColorMode.CyclePerLine)
        {
            int idx = Mod(lineCount - currentLine - 1, k);
            return _lineColors[idx];
        }

        if (lineCount <= 1)
            return _lineColors[0];

        int last = k - 1;
        float t = (currentLine - 1f) / (lineCount - 1f);   // 0 ~ 1

        // 구간 스냅: 가장 가까운 팔레트 색
        if (_colorMode == LineColorMode.DistributeStepped)
            return _lineColors[Mathf.Clamp(Mathf.RoundToInt(t * last), 0, last)];

        // 보간 분배
        float scaled = t * last;                            // 0 ~ (색 개수-1)
        int i = Mathf.FloorToInt(scaled);
        int j = Mathf.Min(i + 1, last);
        return Color.Lerp(_lineColors[i], _lineColors[j], scaled - i);
    }

    /// <summary>항상 0~m-1 을 반환하는 나머지 연산 (C# % 의 음수 결과 보정).</summary>
    private static int Mod(int a, int m) => ((a % m) + m) % m;

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