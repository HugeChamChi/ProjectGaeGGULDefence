using TMPro;
using UnityEngine;
using VContainer;

/// <summary>
/// 시간 금고(9136) 유예 연출 "시스템 오버라이드" — 남은 시간 0초에서 15초 유예가 열리는 동안 패배를 해킹으로 우회한 화면을 덮는다
/// (HTML 시안 C, 2026-11-05 사용자 확정. 기준 design/gdd/overdrive-deck.md C06).
/// TimerController.OnTimeVaultChanged(true)에서 시작, 남은 시간은 TimerController.RemainingTime을 그대로 읽고(보스 사이 정지 포함),
/// OnTimeVaultChanged(false)에서 ExitSeconds 동안 걷어낸다. 그 뒤는 기존 전투 종료 흐름(GameManager.EndGame → DefeatPresentation → 결과)이 이어간다.
/// 숫자는 인게임 타이머가 보여 주고, 여기서는 타이머 색·글리치, 타이머 아래 배지, 잠긴 HUD 흐림과 ACCESS DENIED 띠, 오버레이만 맡는다.
/// 조작 차단은 게임 쪽이 이미 한다. 화면을 덮는 RectTransform(전용 Screen Space Overlay 캔버스 안)에 붙인다. 시간은 timeScale을 무시한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class TimeVaultFx : MonoBehaviour
{
    private readonly PresentationRandom _presentationRandom = new PresentationRandom();
    private static readonly int RectSizeId = Shader.PropertyToID("_RectSize");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
    private static readonly int ScanId = Shader.PropertyToID("_Scan");
    private static readonly int ScanColorId = Shader.PropertyToID("_ScanColor");
    private static readonly int GlitchId = Shader.PropertyToID("_Glitch");
    private static readonly int GlitchSeedId = Shader.PropertyToID("_GlitchSeed");

    // 시안 타임라인(초). 자주 만질 값은 TimeVaultFxSettings.
    private const float TerminalLineStep = 0.25f, TerminalLineFirst = 0.1f, TerminalTypeSeconds = 0.28f, TerminalEnd = 2.2f, TerminalFade = 0.33f;
    private const float TitleStart = 1.15f, TitleHold = 1.1f, DenyStart = 0.3f, BadgeStart = 1.4f, BadgeGap = 10f;
    private const float CopyJitterPixels = 16f, CopyJitterStep = 0.05f, CopyOffset = 12f, TimerJitterPixels = 10f;
    private const float HurryBlinkHz = 2f, FlickerHz = 0.42f;
    private const float Tint = 0.08f, Scan = 0.12f, Vignette = 0.4f, OverlayFade = 0.2f, LockFadeStart = 0.9f, LockFade = 0.3f;

    [SerializeField] private TimeVaultFxSettings _settings;
    [Tooltip("유예 중 흐리게 보여 줄 잠긴 HUD. 하단 버튼 묶음의 CanvasGroup은 CombatHudPager가 넘김에 쓰므로 버튼마다 넣는다")]
    [SerializeField] private CanvasGroup[] _lockTargets;
    [Tooltip("ACCESS DENIED 띠가 덮을 영역 (하단 버튼 묶음). 사각형 높이가 0이면 켜진 자식 버튼 전체를 감싼다")]
    [SerializeField] private RectTransform _denyArea;
    [Tooltip("인게임 타이머 패널 — 배지를 그 아래에 붙인다")]
    [SerializeField] private RectTransform _timerAnchor;
    [Tooltip("인게임 타이머 글자 — 유예 중 빨갛게, 글리치 때 흔든다")]
    [SerializeField] private TMP_Text _timerText;
    [Tooltip("글리치 때 흔들 HUD 루트 (비우면 흔들지 않음)")]
    [SerializeField] private RectTransform _shakeTarget;

    private TimerController _timer;
    private RectTransform _rect;
    private CanvasGroup _rootGroup;
    private TimeVaultFxView _view;
    private float[] _lockAlpha;
    private Vector2 _shakeHome, _timerTextHome;
    private Color _timerColor;
    private float _grace = 1f, _remaining;
    private float _startTime, _exitTime, _glitchUntil;
    private bool _active, _exiting;

    /// <summary>유예 연출이 화면에 떠 있는지 (걷어내는 중 포함).</summary>
    public bool IsActive => _active;

    /// <summary>씬 LifetimeScope가 넣어 준다 (InGameLifetimeScope의 선택 등록).</summary>
    [Inject]
    public void Construct(TimerController timer)
    {
        if (_timer != null) _timer.OnTimeVaultChanged -= HandleVaultChanged;
        _timer = timer;
        if (_timer == null) return;
        _timer.OnTimeVaultChanged += HandleVaultChanged;
        if (_timer.TimeVaultActive) HandleVaultChanged(true);
    }

    /// <summary>
    /// 인게임 HUD 참조를 코드로 넣는다 (HUD가 다른 씬에 있거나 런타임에 옮겨지는 경우). 같은 씬이면 인스펙터에 넣어도 된다.
    /// 유예 중에는 바꾸지 않는다.
    /// </summary>
    public void BindHud(CanvasGroup[] lockTargets, RectTransform denyArea, RectTransform timerAnchor, TMP_Text timerText, RectTransform shakeTarget)
    {
        if (_active) return;
        _lockTargets = lockTargets;
        _denyArea = denyArea;
        _timerAnchor = timerAnchor;
        _timerText = timerText;
        _shakeTarget = shakeTarget;
    }

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    private void OnDestroy()
    {
        if (_timer != null) _timer.OnTimeVaultChanged -= HandleVaultChanged;
        Hide();
        if (_view?.OverlayMaterial != null) Destroy(_view.OverlayMaterial);
    }

    private void HandleVaultChanged(bool active)
    {
        if (active) Begin(_timer.RemainingTime);
        else End();
    }

    /// <summary>유예 시작 연출. 이미 떠 있으면 처음부터.</summary>
    /// <param name="graceSeconds">유예 길이 (글리치 빈도 비율 계산용)</param>
    public void Begin(float graceSeconds)
    {
        if (_settings == null) { Debug.LogError("[TimeVaultFx] 설정 SO가 없습니다.", this); return; }
        if (_active) Hide();
        EnsureView();
        _grace = Mathf.Max(0.01f, graceSeconds);
        _remaining = _grace;
        _startTime = Time.unscaledTime;
        _glitchUntil = _startTime + _settings.TriggerGlitchSeconds;
        _exiting = false;
        _active = true;
        _rootGroup.alpha = 1f;
        SetVisible(true);

        _lockAlpha = new float[_lockTargets?.Length ?? 0];
        for (int i = 0; i < _lockAlpha.Length; i++) _lockAlpha[i] = _lockTargets[i] != null ? _lockTargets[i].alpha : 1f;
        if (_shakeTarget != null) _shakeHome = _shakeTarget.anchoredPosition;
        if (_timerText != null) { _timerColor = _timerText.color; _timerTextHome = _timerText.rectTransform.anchoredPosition; }
        Tick();
    }

    /// <summary>남은 유예 시간(초). TimerController에 묶여 있으면 매 프레임 자동으로 읽는다.</summary>
    public void SetRemaining(float seconds)
    {
        _remaining = Mathf.Clamp(seconds, 0f, _grace);
    }

    /// <summary>유예가 끝났다 — 마지막 글리치와 함께 ExitSeconds 동안 걷어내고 꺼진다. 전투 종료 흐름은 건드리지 않는다.</summary>
    public void End()
    {
        if (!_active || _exiting) return;
        _exiting = true;
        _exitTime = Time.unscaledTime;
        _remaining = 0f;
        _glitchUntil = _exitTime + _settings.ExitSeconds;
    }

    /// <summary>즉시 모두 끄고 손댄 HUD(잠금 알파·타이머 색·흔든 위치)를 되돌린다.</summary>
    public void Hide()
    {
        if (_view == null) return;
        _active = false;
        _exiting = false;
        SetVisible(false);
        if (_lockAlpha != null)
            for (int i = 0; i < _lockAlpha.Length; i++)
                if (_lockTargets[i] != null) _lockTargets[i].alpha = _lockAlpha[i];
        if (_shakeTarget != null) _shakeTarget.anchoredPosition = _shakeHome;
        if (_timerText != null) { _timerText.color = _timerColor; _timerText.rectTransform.anchoredPosition = _timerTextHome; }
    }

    private void SetVisible(bool on)
    {
        _view.Overlay.gameObject.SetActive(on);
        _view.TerminalGroup.gameObject.SetActive(on);
        _view.TitleGroup.gameObject.SetActive(on);
        _view.Deny.gameObject.SetActive(on);
        _view.Badge.gameObject.SetActive(on);
    }

    private void Update()
    {
        if (!_active) return;
        if (!_exiting && _timer != null && _timer.TimeVaultActive) _remaining = Mathf.Clamp(_timer.RemainingTime, 0f, _grace);
        Tick();
    }

    private void EnsureView()
    {
        if (_view != null) return;
        if (_rect == null) _rect = (RectTransform)transform;
        _rootGroup = GetComponent<CanvasGroup>();
        if (_rootGroup == null) _rootGroup = gameObject.AddComponent<CanvasGroup>();
        _rootGroup.blocksRaycasts = false;
        _rootGroup.interactable = false;
        _view = new TimeVaultFxView(_rect, _settings);
        if (_view.OverlayMaterial != null) _view.OverlayMaterial.SetColor(ScanColorId, _settings.OverrideColor);
        SetVisible(false);
    }

    private void Tick()
    {
        var s = _settings;
        var v = _view;
        float now = Time.unscaledTime;
        float it = now - _startTime;
        float exitK = _exiting ? Mathf.Clamp01((now - _exitTime) / Mathf.Max(0.01f, s.ExitSeconds)) : 0f;
        if (_exiting && exitK >= 1f) { Hide(); return; }
        _rootGroup.alpha = 1f - exitK;

        // 글리치: 발동·종료 때 한 번 길게, 유지 중에는 남은 시간이 줄수록 잦게
        if (!_exiting && now >= _glitchUntil)
        {
            float p = 1f - _remaining / _grace;
            float rate = Mathf.Lerp(s.GlitchRate.x, s.GlitchRate.y, p * p);
            if (_presentationRandom.Value < rate * Time.unscaledDeltaTime)
                _glitchUntil = now + _presentationRandom.Range(s.GlitchBurstSeconds.x, s.GlitchBurstSeconds.y);
        }
        bool glitching = now < _glitchUntil;
        if (_shakeTarget != null)
            _shakeTarget.anchoredPosition = _shakeHome + (glitching ? new Vector2(_presentationRandom.Range(-1f, 1f), _presentationRandom.Range(-0.3f, 0.3f)) * s.GlitchShakePixels : Vector2.zero);

        // 잠긴 HUD 흐림 · 타이머 빨강 (걷어낼 때 되돌린다)
        float hold = Fade(it, LockFadeStart, LockFade) * (1f - exitK);
        for (int i = 0; i < _lockAlpha.Length; i++)
            if (_lockTargets[i] != null) _lockTargets[i].alpha = Mathf.Lerp(_lockAlpha[i], s.LockedAlpha, hold);
        if (_timerText != null)
        {
            _timerText.color = Color.Lerp(_timerColor, s.OverrideColor, Fade(it, 0f, LockFade) * (1f - exitK));
            _timerText.rectTransform.anchoredPosition = _timerTextHome + (glitching ? new Vector2(_presentationRandom.Range(-1f, 1f) * TimerJitterPixels, 0f) : Vector2.zero);
        }

        // 발동: 터미널 4줄 → OVERRIDE
        float termK = it < TerminalEnd - TerminalFade ? 1f : 1f - Mathf.Clamp01((it - (TerminalEnd - TerminalFade)) / TerminalFade);
        v.TerminalGroup.alpha = it < TerminalEnd ? termK : 0f;
        for (int i = 0; i < v.TerminalLines.Length; i++)
        {
            float k = Mathf.Clamp01((it - TerminalLineFirst - i * TerminalLineStep) / TerminalTypeSeconds);
            var line = v.TerminalLines[i];
            line.maxVisibleCharacters = Mathf.RoundToInt(line.text.Length * k);
        }
        v.TitleGroup.alpha = FadeHold(it, TitleStart, TitleHold);
        JitterCopies(v.Title);

        // 유지: ACCESS DENIED 띠, 타이머 아래 배지 (남은 시간이 적으면 깜빡임)
        PlaceDeny();
        v.DenyGroup.alpha = Fade(it, DenyStart, 0.1f) * Flicker(it);
        PlaceBadge();
        bool hurry = _remaining <= s.HurrySeconds && !_exiting;
        v.BadgeGroup.alpha = Fade(it, BadgeStart, 0.3f) * (hurry && Mathf.Repeat(now * HurryBlinkHz, 1f) > 0.5f ? 0.35f : 1f);

        SetOverlay(Fade(it, 0f, OverlayFade), glitching);
    }

    // 배지를 인게임 타이머 패널 바로 아래에 붙인다 (둘 다 Screen Space Overlay라 월드 좌표 = 화면 좌표).
    private void PlaceBadge()
    {
        if (_timerAnchor == null) return;
        var corners = new Vector3[4];
        _timerAnchor.GetWorldCorners(corners);
        _view.Badge.position = (corners[0] + corners[3]) * 0.5f;
        _view.Badge.anchoredPosition += new Vector2(0f, -(_view.Badge.sizeDelta.y * 0.5f + BadgeGap));
    }

    // ACCESS DENIED 띠를 잠긴 하단 버튼 묶음 크기에 맞춘다. 묶음 사각형이 납작하면(높이 0) 켜진 자식 버튼 전체를 감싼다.
    private void PlaceDeny()
    {
        if (_denyArea == null) return;
        var corners = new Vector3[4];
        _denyArea.GetWorldCorners(corners);
        Vector2 min = corners[0], max = corners[2];
        if (max.y - min.y < 1f)
        {
            min = new Vector2(float.MaxValue, float.MaxValue); max = new Vector2(float.MinValue, float.MinValue);
            foreach (RectTransform child in _denyArea)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                child.GetWorldCorners(corners);
                min = Vector2.Min(min, corners[0]); max = Vector2.Max(max, corners[2]);
            }
            if (max.x < min.x) return;
        }
        float scale = Mathf.Max(0.0001f, _rect.lossyScale.x);
        _view.Deny.position = (min + max) * 0.5f;
        _view.Deny.sizeDelta = (max - min) / scale;
    }

    private void SetOverlay(float k, bool glitching)
    {
        var m = _view.OverlayMaterial;
        if (m == null) return;
        var tint = _settings.OverrideColor; tint.a = Tint * k;
        m.SetVector(RectSizeId, _rect.rect.size);
        m.SetColor(TintId, tint);
        m.SetFloat(VignetteId, Vignette * k);
        m.SetFloat(ScanId, Scan * k);
        m.SetFloat(GlitchId, glitching ? 1f : 0f);
        if (glitching) m.SetFloat(GlitchSeedId, Mathf.Floor(Time.unscaledTime / CopyJitterStep));
    }

    // 복제 글자 두 장을 일정 간격으로 무작위 이동 — CSS 시안의 clip 슬라이스 대신.
    private static void JitterCopies(TextMeshProUGUI[] copies)
    {
        var basePos = copies[0].rectTransform.anchoredPosition;
        float step = Mathf.Floor(Time.unscaledTime / CopyJitterStep);
        for (int i = 1; i < copies.Length; i++)
        {
            float side = i == 1 ? -1f : 1f;
            float jx = (Hash(step * 1.7f + i) - 0.5f) * 2f * CopyJitterPixels;
            float jy = (Hash(step * 3.1f + i * 5f) - 0.5f) * CopyJitterPixels * 0.5f;
            copies[i].rectTransform.anchoredPosition = basePos + new Vector2(side * CopyOffset + jx, jy);
            var c = copies[i].color; c.a = 0.55f + 0.45f * Hash(step + i * 11f); copies[i].color = c;
        }
    }

    private static float Fade(float t, float start, float seconds) => Mathf.Clamp01((t - start) / Mathf.Max(0.0001f, seconds));

    // 12%에 다 보이고 82%부터 사라지는 등장-유지-퇴장 곡선 (시안 fadeHold).
    private static float FadeHold(float t, float start, float total)
    {
        float k = (t - start) / total;
        if (k <= 0f || k >= 1f) return 0f;
        if (k < 0.12f) return k / 0.12f;
        if (k > 0.82f) return 1f - (k - 0.82f) / 0.18f;
        return 1f;
    }

    // 가끔 깜빡이는 계단식 알파 (시안 flick).
    private static float Flicker(float t)
    {
        float k = Mathf.Repeat(t * FlickerHz, 1f);
        if (k > 0.31f && k < 0.33f) return 0.35f;
        if (k > 0.71f && k < 0.72f) return 0.5f;
        return 1f;
    }

    private static float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f, 1f);
}
