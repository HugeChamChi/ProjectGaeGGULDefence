using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using DG.Tweening;

/// <summary>전체 또는 현재 줄의 체력 비율과 남은 체력줄을 표시하는 보스 체력바.</summary>
public class UI_BossHpBar : MonoBehaviour
{
    [Header("Debuffs")]
    [SerializeField] private Vector2 _debuffOffset = new Vector2(0f, -10f);
    [SerializeField] private Vector2 _debuffSlotSize = new Vector2(80f, 80f);
    [Tooltip("디버프 표시 영역. 자식 BossDebuffs의 RectTransform으로 위치를 조절합니다.")]
    [SerializeField] private BossDebuffBar _debuffBar;

    /// <summary>Connects active boss effects to the status row below this HP bar.</summary>
    public void ConfigureDebuffs(BossManager manager, DebuffSettings settings)
    {
        if (_debuffBar == null) _debuffBar = GetComponentInChildren<BossDebuffBar>(true);
        if (_debuffBar == null)
        {
            var row = new GameObject("BossDebuffs", typeof(RectTransform), typeof(BossDebuffBar));
            row.transform.SetParent(transform, false);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = _debuffOffset;
            rect.sizeDelta = new Vector2(400f, _debuffSlotSize.y);
            _debuffBar = row.GetComponent<BossDebuffBar>();
        }
        _debuffBar.Configure(manager, settings, _debuffSlotSize, _hpLineText != null ? _hpLineText.font : null);
    }
    [Header("Bar")]
    [Tooltip("HP에 따라 가로 길이가 줄어드는 Mask 영역")]
    [SerializeField] private RectTransform _hpFillMask;

    [Tooltip("실제 HP 색상을 담당")]
    [SerializeField] private Image _hpColor;

    [Tooltip("채움의 좌/우 표시 여백 (그림 원본 픽셀 기준). 투명 끝부분과 보스 아이콘 뒤에 가려진 부분을 제외한다")]
    [SerializeField] private Vector2 _fillSpriteHorizontalPadding;
    [Tooltip("왼쪽을 가리는 보스 아이콘. 화면비가 달라져도 이 영역 오른쪽부터 채움을 표시한다")]
    [SerializeField] private RectTransform _fillStartAfter;
    [SerializeField, Min(0f)] private float _fillStartGap = 1f;
    private readonly Vector3[] _fillStartCorners = new Vector3[4];


    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI _hpLineText;


    [Header("HP Lines")]
    [Min(1)]
    [Tooltip("총 체력줄 수 (예: 100). 색은 아래 Line Colors 를 순서대로 분배·보간")]
    [SerializeField] private int _lineCount = 5;
    [Tooltip("현재 남은 한 줄의 HP 비율로 채운다. 끄면 기존 전체 HP 비율로 표시한다.")]
    [SerializeField] private bool _fillCurrentLine;
    [Tooltip("HP 몸통 배경. 채움·다음 줄 그림을 이 배경과 같은 가로 자리·폭에 그려 정확히 겹친다. 비우면 채움 부모 폭을 쓴다.")]
    [SerializeField] private Image _hpBackground;
    [Tooltip("남은 보호막을 HP 위에 겹쳐 그리는 띠. 비우면 보호막을 표시하지 않는다")]
    [SerializeField] private UI_BossHpShield _shield;
    private RectTransform _nextLineMask;
    private Image _nextLineFill;
    private readonly Vector3[] _backgroundCorners = new Vector3[4];

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
        new Color(0.36f, 0.62f, 0.38f), // Green (채도·밝기를 낮춘 눈부심 완화 팔레트)
        new Color(0.78f, 0.65f, 0.32f), // Yellow
        new Color(0.78f, 0.48f, 0.30f), // Orange
        new Color(0.70f, 0.30f, 0.32f), // Red
        new Color(0.52f, 0.39f, 0.72f), // Purple
    };
    

    [Header("Text Format")]
    [Tooltip("줄 수 앞에 붙는 접두사 (예: x182)")]
    [FormerlySerializedAs("_lineSuffix")]
    [SerializeField] private string _linePrefix = "x";

    [Header("HP Per Line")]
    [Tooltip("한 줄이 담당하는 체력량. BeginBoss에 줄 수를 명시하지 않으면(0 이하) 이 값으로 maxHp에서 자동 계산한다.")]
    [SerializeField, Min(1)] private long _hpPerLine = 500;


    [Header("Damage Tween (DOTween)")]
    [Tooltip("피해 시 바/퍼센트를 부드럽게 보간")]
    [SerializeField] private bool _smooth = true;
    [SerializeField] private float _tweenDuration = 0.3f;
    [Tooltip("피격 직후 빠르게 감소하고 끝에서 부드럽게 멈추는 보간 곡선")]
    [SerializeField] private Ease _damageEase = Ease.OutCubic;

    private float _shownRatio;
    private bool _initialized;
    private Tween _barTween;
    private decimal _currentHp;
    private decimal _maxHp = 1m;
    private decimal _runtimeHpPerLine;
    private float _targetRatio;


    /// <summary>전체 체력줄 수.</summary>
    public int LineCount => Mathf.Max(1, _lineCount);

    /// <summary>실제 보스가 이 바를 소유하면 독립 테스트 도구는 값을 덮어쓰지 않는다.</summary>
    public bool IsRuntimeControlled { get; private set; }

    /// <summary>보스 교체/줄 수 재설정 알림. 피해 연출의 기준을 초기화한다.</summary>
    public event Action OnHpReset;

    /// <summary>현재 남은 체력줄 (0 = 사망).</summary>
    public int CurrentLine => _currentLine;
    private int _currentLine = -1;

    /// <summary>현재 줄이 변경될 때 새 줄 번호와 함께 발생 (흔들림 연출 등 구독용).</summary>
    public event Action<int> OnCurrentLineChanged;


    private void OnDestroy() => _barTween?.Kill();

    private void OnRectTransformDimensionsChange()
    {
        if (_initialized) ApplyBarVisual(_shownRatio);
    }

    /// <summary>런타임에서 총 체력줄 수 변경 (디버그/설정용). 이후 SetHp 호출 시 즉시 반영.</summary>
    public void SetLineCount(int count)
    {
        _runtimeHpPerLine = 0m;
        ResetHp(_currentHp, _maxHp, count);
    }

    /// <summary>독립 테스트 도구에서 사용하는 실수 HP 입력.</summary>
    public void SetHp(float currentHp, float maxHp)
    {
        SetHpExact((decimal)currentHp, (decimal)maxHp);
    }

    /// <summary>실제 전투의 decimal HP로 줄 수를 계산하고 표시 단계에서만 float로 변환한다.</summary>
    public void SetHpExact(decimal currentHp, decimal maxHp)
    {
        decimal nextMaxHp = maxHp > 0m ? maxHp : 1m;
        decimal nextHp = Math.Min(nextMaxHp, Math.Max(0m, currentHp));
        // 동일 HP 알림이 보간을 매번 다시 시작해 끝까지 도달하지 못하는 것을 막는다.
        if (_initialized && nextHp == _currentHp && nextMaxHp == _maxHp) return;
        _maxHp = nextMaxHp;
        _currentHp = nextHp;
        decimal exactRatio = _currentHp / _maxHp;
        float ratio = (float)exactRatio;
        _targetRatio = ratio;

        // 이벤트/줄 판정은 목표값 기준으로 즉시 (흔들림·유리깨짐이 바로 터지게)
        int targetLine =
            _currentHp <= 0m
                ? 0
                : Math.Max(1, (int)decimal.Ceiling(LineUnits(_currentHp)));

        if (targetLine != _currentLine)
        {
            _currentLine = targetLine;
            OnCurrentLineChanged?.Invoke(targetLine);
        }

        // 바/퍼센트는 부드럽게 보간 (첫 세팅·에디터·비활성 시엔 즉시)
        _barTween?.Kill();

        if (_smooth && _initialized && Application.isPlaying && isActiveAndEnabled)
        {
            _barTween = DOTween.To(
                    () => _shownRatio,
                    v => { _shownRatio = v; ApplyBarVisual(v); },
                    ratio,
                    _tweenDuration)
                .SetUpdate(true)
                .SetEase(_damageEase);
        }
        else
        {
            _shownRatio = ratio;
            ApplyBarVisual(ratio);
        }

        _initialized = true;
    }

    /// <summary>보스의 남은 보호막을 표시한다. 늘면 차오름, 줄면 HP와 같은 보간, 0이면 깨짐 연출.</summary>
    public void SetShieldExact(decimal shield, decimal maxHp)
    {
        if (_shield == null) return;
        decimal perLine = _runtimeHpPerLine > 0m ? _runtimeHpPerLine : (maxHp > 0m ? maxHp : 1m) / LineCount;
        _shield.SetShieldExact(shield, maxHp, perLine);
    }

    /// <summary>독립 테스터보다 먼저 실제 보스용 표시로 지정한다.</summary>
    public void UseRuntimeData() => IsRuntimeControlled = true;

    /// <summary>새 보스를 즉시 표시하고 이전 보스의 보간/피격 연출을 초기화한다.
    /// lineCount를 0 이하로 주면(생략 포함) <see cref="_hpPerLine"/> 기준 maxHp에서 자동 계산한다.</summary>
    public void BeginBoss(decimal currentHp, decimal maxHp, int lineCount = 0, decimal hpPerLine = 0m)
    {
        UseRuntimeData();
        _runtimeHpPerLine = Math.Max(0m, hpPerLine);
        ResetHp(currentHp, maxHp, ResolveLineCount(maxHp, lineCount));
        // 이전 보스의 보호막은 연출 없이 지운다.
        _shield?.ClearImmediate();
    }

    /// <summary>lineCount가 명시(1 이상)되면 그대로, 아니면 hpPerLine 기준으로 maxHp에서 계산한다.</summary>
    private int ResolveLineCount(decimal maxHp, int lineCount)
    {
        if (_runtimeHpPerLine > 0m) return (int)Math.Max(1m, decimal.Ceiling(maxHp / _runtimeHpPerLine));
        if (lineCount > 0) return lineCount;
        long perLine = Math.Max(1, _hpPerLine);
        return (int)Math.Max(1m, Math.Ceiling(maxHp / perLine));
    }

    private void ResetHp(decimal currentHp, decimal maxHp, int lineCount)
    {
        _barTween?.Kill();
        _lineCount = Mathf.Max(1, lineCount);
        _maxHp = maxHp > 0m ? maxHp : 1m;
        _currentHp = Math.Min(_maxHp, Math.Max(0m, currentHp));
        decimal ratio = _currentHp / _maxHp;
        _targetRatio = _shownRatio = (float)ratio;
        _currentLine = _currentHp <= 0m ? 0 : Math.Max(1, (int)decimal.Ceiling(LineUnits(_currentHp)));
        _initialized = true;
        ApplyBarVisual(_shownRatio);
        OnHpReset?.Invoke();
    }

    private decimal LineUnits(decimal hp) => _runtimeHpPerLine > 0m
        ? hp / _runtimeHpPerLine : hp / _maxHp * LineCount;

    /// <summary>보간된 표시 비율(shownRatio) 기준으로 바 길이·색·텍스트를 갱신.</summary>
    private void ApplyBarVisual(float shownRatio)
    {
        int lineCount = LineCount;
        float shownLineUnits = _runtimeHpPerLine > 0m
            ? shownRatio * (float)(_maxHp / _runtimeHpPerLine) : shownRatio * lineCount;
        int shownLine = shownRatio == _targetRatio && _currentLine >= 0 ? _currentLine :
            shownRatio <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(shownLineUnits), 1, lineCount);
        // 줄 단위 채움은 3겹이다: 몸통 배경(전체 폭) > 다음 줄(전체 HP 비율 폭) > 현재 줄.
        // 현재 줄은 전체 HP 비율 구간 안에서 남은 한 줄 비율만큼 그려, 깎인 오른쪽에 배경이 드러난다.
        float fillRatio = _fillCurrentLine && shownLine > 0
            ? shownRatio * Mathf.Clamp01(shownLineUnits - (shownLine - 1)) : shownRatio;

        // 표시 비율만큼 Mask 가로 길이 (왼쪽 고정, 오른쪽이 줄어듦).
        // 앵커 기반이라 Pivot 설정과 무관하게 왼쪽 정렬 + 부모 폭 상대.
        Vector2 padding = Vector2.zero;
        float coverStart = 0f;
        float artLeft = 0f, artWidth = 0f;
        if (_hpFillMask != null)
        {
            var fullBar = _hpFillMask.parent as RectTransform;
            if (fullBar != null)
            {
                // 채움 그림의 가로 자리·폭은 몸통 배경과 같게 (튜토리얼은 배경이 아이콘 밑까지 더 길다).
                artWidth = fullBar.rect.width;
                if (_hpBackground != null)
                {
                    _hpBackground.rectTransform.GetWorldCorners(_backgroundCorners);
                    float left = fullBar.InverseTransformPoint(_backgroundCorners[0]).x;
                    float right = fullBar.InverseTransformPoint(_backgroundCorners[2]).x;
                    artLeft = left - fullBar.rect.xMin;
                    artWidth = right - left;
                }
            }
            if (fullBar != null && _hpColor != null && _hpColor.sprite != null)
            {
                float pixels = _hpColor.sprite.rect.width;
                if (pixels > 0f)
                {
                    padding = _fillSpriteHorizontalPadding * (artWidth / pixels);
                    padding.x += artLeft;
                    padding.y += fullBar.rect.width - (artLeft + artWidth);
                }
            }
            if (fullBar != null && _fillStartAfter != null)
            {
                _fillStartAfter.GetWorldCorners(_fillStartCorners);
                float right = float.NegativeInfinity;
                foreach (var corner in _fillStartCorners)
                    right = Mathf.Max(right, fullBar.InverseTransformPoint(corner).x);
                padding.x = Mathf.Max(padding.x, right - fullBar.rect.xMin + _fillStartGap);
            }
            if (fullBar != null)
                padding.x = Mathf.Clamp(padding.x, 0f, Mathf.Max(0f, fullBar.rect.width - padding.y));
            // 비율은 아이콘 오른쪽부터 재되, 칠하기는 위에 그려지는 둥근 아이콘 중심부터 시작해
            // 원 테두리가 휘는 자리에 배경이 비치지 않게 한다.
            coverStart = padding.x;
            // HP 0에서는 아이콘 밑 칠하기도 없앤다. 원형 아이콘이 직사각형 모서리를 다 덮지 못해 마지막 줄 색이 비친다.
            if (fullBar != null && shownLine > 0 && IconDrawsAboveBar())
            {
                float center = fullBar.InverseTransformPoint(_fillStartAfter.TransformPoint(_fillStartAfter.rect.center)).x;
                coverStart = Mathf.Clamp(center - fullBar.rect.xMin, 0f, padding.x);
            }
            Vector2 anchorMin = _hpFillMask.anchorMin;
            Vector2 anchorMax = _hpFillMask.anchorMax;
            anchorMin.x = 0f;
            anchorMax.x = fillRatio;
            _hpFillMask.anchorMin = anchorMin;
            _hpFillMask.anchorMax = anchorMax;

            Vector2 offsetMin = _hpFillMask.offsetMin;
            Vector2 offsetMax = _hpFillMask.offsetMax;
            offsetMin.x = coverStart;
            offsetMax.x = Mathf.Lerp(padding.x, -padding.y, fillRatio);
            _hpFillMask.offsetMin = offsetMin;
            _hpFillMask.offsetMax = offsetMax;

            // Keep the artwork at the full bar width while the mask clips it.
            // A percentage anchor plus fixed pixels only matched one screen size.
            if (_hpColor != null && _hpColor.rectTransform.parent == _hpFillMask && fullBar != null)
                _hpColor.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left,
                    artLeft - coverStart, artWidth);
        }

        if (_hpColor != null && shownLine >= 1)
            _hpColor.color = EvaluateLineColor(shownLine, lineCount);
        ApplyNextLine(shownLine, lineCount, shownRatio, padding, coverStart, artLeft, artWidth);

        if (_hpLineText != null)
            _hpLineText.text = _linePrefix + shownLine;
    }


    // 줄 단위 채움에서 깎인 자리에 다음 줄 색을 보여 준다. 마지막 줄이면 배경이 드러난다.
    // 다음 줄 층은 전체 HP 비율 폭까지만 채움과 같은 마스크·그림 배치로 그린다.
    // 그래서 현재 줄이 꽉 차 있으면 다음 줄 색이 가장자리로 한 픽셀도 새지 않는다.
    private void ApplyNextLine(int shownLine, int lineCount, float totalRatio, Vector2 padding, float coverStart, float artLeft, float artWidth)
    {
        var fullBar = _hpFillMask != null ? _hpFillMask.parent as RectTransform : null;
        bool visible = _fillCurrentLine && shownLine > 1 && _hpColor != null && fullBar != null;
        if (!visible)
        {
            if (_nextLineFill != null) _nextLineFill.enabled = false;
            return;
        }
        if (_nextLineFill == null)
        {
            var mask = new GameObject("HpNextLineMask", typeof(RectTransform), typeof(RectMask2D));
            mask.transform.SetParent(fullBar, false);
            mask.transform.SetSiblingIndex(_hpFillMask.GetSiblingIndex());
            _nextLineMask = (RectTransform)mask.transform;
            var go = new GameObject("HpNextLine", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_nextLineMask, false);
            _nextLineFill = go.GetComponent<Image>();
            _nextLineFill.raycastTarget = false;
        }
        _nextLineFill.sprite = _hpColor.sprite;
        _nextLineFill.type = _hpColor.type;
        _nextLineFill.pixelsPerUnitMultiplier = _hpColor.pixelsPerUnitMultiplier;
        _nextLineFill.material = _hpColor.material;

        // 채움 마스크의 fillRatio = 전체 HP 비율 상태와 같은 영역.
        _nextLineMask.pivot = _hpFillMask.pivot;
        _nextLineMask.anchorMin = new Vector2(0f, _hpFillMask.anchorMin.y);
        _nextLineMask.anchorMax = new Vector2(totalRatio, _hpFillMask.anchorMax.y);
        _nextLineMask.offsetMin = new Vector2(coverStart, _hpFillMask.offsetMin.y);
        _nextLineMask.offsetMax = new Vector2(Mathf.Lerp(padding.x, -padding.y, totalRatio), _hpFillMask.offsetMax.y);
        _nextLineMask.localScale = _hpFillMask.localScale;

        // 채움 그림과 같은 배치(전체 바 폭, 왼쪽 여백만큼 당김).
        var from = _hpColor.rectTransform;
        var rect = _nextLineFill.rectTransform;
        rect.pivot = from.pivot;
        rect.anchorMin = from.anchorMin;
        rect.anchorMax = from.anchorMax;
        rect.offsetMin = from.offsetMin;
        rect.offsetMax = from.offsetMax;
        if (from.parent == _hpFillMask)
            rect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, artLeft - coverStart, artWidth);
        _nextLineFill.enabled = true;
        _nextLineFill.color = EvaluateLineColor(shownLine - 1, lineCount);
    }

    // 아이콘이 이 바와 같은 부모에서 뒤 순서(위에 그려짐)일 때만 아이콘 밑으로 칠을 넣는다.
    private bool IconDrawsAboveBar()
    {
        if (_fillStartAfter == null) return false;
        var bar = transform;
        return _fillStartAfter.parent == bar.parent && _fillStartAfter.GetSiblingIndex() > bar.GetSiblingIndex();
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

}
