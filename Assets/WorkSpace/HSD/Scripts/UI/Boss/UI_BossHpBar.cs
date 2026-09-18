using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using DG.Tweening;

/// <summary>전체 체력 비율과 남은 체력줄을 표시하는 보스 체력바.</summary>
public class UI_BossHpBar : MonoBehaviour
{
    [Header("Debuffs")]
    [SerializeField] private Vector2 _debuffOffset = new Vector2(0f, -10f);
    [SerializeField] private Vector2 _debuffSlotSize = new Vector2(58f, 58f);
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


    [Header("Texts")]
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

    private float _shownRatio;
    private bool _initialized;
    private Tween _barTween;
    private decimal _currentHp;
    private decimal _maxHp = 1m;
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

    /// <summary>런타임에서 총 체력줄 수 변경 (디버그/설정용). 이후 SetHp 호출 시 즉시 반영.</summary>
    public void SetLineCount(int count)
    {
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
        _maxHp = maxHp > 0m ? maxHp : 1m;
        _currentHp = Math.Min(_maxHp, Math.Max(0m, currentHp));
        decimal exactRatio = _currentHp / _maxHp;
        float ratio = (float)exactRatio;
        _targetRatio = ratio;

        // 이벤트/줄 판정은 목표값 기준으로 즉시 (흔들림·유리깨짐이 바로 터지게)
        int targetLine =
            _currentHp <= 0m
                ? 0
                : Math.Max(1, (int)decimal.Ceiling(exactRatio * LineCount));

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
                .SetEase(Ease.OutQuad);
        }
        else
        {
            _shownRatio = ratio;
            ApplyBarVisual(ratio);
        }

        _initialized = true;
    }

    /// <summary>독립 테스터보다 먼저 실제 보스용 표시로 지정한다.</summary>
    public void UseRuntimeData() => IsRuntimeControlled = true;

    /// <summary>새 보스를 즉시 표시하고 이전 보스의 보간/피격 연출을 초기화한다.
    /// lineCount를 0 이하로 주면(생략 포함) <see cref="_hpPerLine"/> 기준 maxHp에서 자동 계산한다.</summary>
    public void BeginBoss(decimal currentHp, decimal maxHp, int lineCount = 0)
    {
        UseRuntimeData();
        ResetHp(currentHp, maxHp, ResolveLineCount(maxHp, lineCount));
    }

    /// <summary>lineCount가 명시(1 이상)되면 그대로, 아니면 hpPerLine 기준으로 maxHp에서 계산한다.</summary>
    private int ResolveLineCount(decimal maxHp, int lineCount)
    {
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
        _currentLine = _currentHp <= 0m ? 0 : Math.Max(1, (int)decimal.Ceiling(ratio * LineCount));
        _initialized = true;
        ApplyBarVisual(_shownRatio);
        OnHpReset?.Invoke();
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

        int shownLine = shownRatio == _targetRatio && _currentLine >= 0 ? _currentLine :
            shownRatio <= 0f
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(shownRatio * lineCount), 1, lineCount);

        if (_hpColor != null && shownLine >= 1)
            _hpColor.color = EvaluateLineColor(shownLine, lineCount);

        if (_hpLineText != null)
            _hpLineText.text = _linePrefix + shownLine;
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
