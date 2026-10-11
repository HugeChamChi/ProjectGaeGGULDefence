using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TimerBonusEase;

/// <summary>
/// 시안 E 해킹 (사용자 요청 2026-10-09) — 레퍼런스 design/글리치모드.gif의 금 간 크리스탈 홀로그램 글리치:
/// 하늘색 가로 스캔라인이 튀고, 화면이 가로 띠 단위로 어긋나며, 색이 번진다.
/// 처치 → 타이머가 깜빡 감염 → 가로 띠로 찢어지며 숫자가 마구 뒤섞이고 진행 막대가 끊기며 차오름
/// → 왼쪽부터 자릿수가 (남은 시간 + 보너스)로 고정 → 마지막 자리에서 튀며 정상화, 잔진동 글리치 두어 번.
/// 찢어진 모습은 가로 띠마다 마스크한 숫자 사본으로 그리고(본체는 숨김), 띠가 어긋나지 않으면 원래 타이머와 똑같이 보인다.
/// 글리치 무늬는 TickSeconds마다 틱 번호 해시로 정해서 같은 a에는 같은 화면이 나온다.
/// 중첩 실험(시안 G~J, TimerBonusHackStack)은 RenderRun으로 해킹 한 번(Run)을 시작 시각·보너스·짧은 버전 여부와 함께 그린다.
/// 상승 모드(시안 F, 사용자 요청 2026-10-09): 뒤섞는 대신 진행 막대가 끊겨 찰 때마다 숫자가 덩어리째 올라가
/// "시간을 주입한다"는 느낌을 준다. 소수 자리만 글리치로 흔들리고, 한 칸 오를 때마다 위로 튀는 불꽃·박동.
/// 인게임(디시그망 시간 회복, 2026-10-09): 실제 HUD 타이머의 글꼴·재질·색으로 그리고, 정수 3자리(100초 이상)를 지원하며,
/// 완료 순간 화면 전체 플래시 없이 연출 시계 히트스톱만 준다 (HackTuning.RuntimeScreenFlash로 다시 켤 수 있음).
/// </summary>
public sealed class TimerBonusHack : ITimerBonusConcept
{
    private const int KeyLockBase = 100;
    private const int KeyDone = 110;
    private const int KeyStepBase = 200;
    private const int KeyRunBase = 10000;
    private const int KeyRunStride = 1000;
    private const int MaxReels = 5;
    private const int MaxSlices = 12;
    private const int MaxScanlines = 16;
    private const int MaxBlocks = 24;
    private const float SliceSpan = 1.3f;             // 띠가 덮는 높이 (숫자 칸 높이 배율, 잔상·흔들림 여유)
    private const float SliceOverlap = 1f;            // 띠 경계 머리카락 틈 방지 (캔버스 단위)
    private const float BandWidthScale = 3f;          // 띠 마스크 폭 (가로로는 자르지 않는다)
    private const float CutJitter = 0.8f;             // 띠 경계 흔들림 폭 (띠 높이 배율, 1 미만이면 경계 순서가 뒤집히지 않음)
    private const float AltDigitChance = 0.3f;        // 띠 하나가 다른 숫자를 보여 줄 확률 (위아래가 다른 숫자인 깨진 글자)
    private const float BarWidthPx = 184f;
    private const float BarHeightPx = 6f;
    private const float BarGapPx = 14f;
    private const int BarSteps = 14;                  // 진행 막대가 끊겨 차는 단계 수
    private const float StutterFrequency = 5.3f;      // 막대 머뭇거림 횟수 (진행률 0~1 동안)
    private const float StutterAmount = 0.8f;         // 머뭇거림 세기 (1 미만이어야 뒤로 가지 않음)
    private const float ChainPunch = 0.3f;            // 이어 붙은 결과 글자가 커졌다 돌아오는 정도
    private const float BarHoldSeconds = 0.2f;
    private const float BarFadeSeconds = 0.15f;
    private const float StateGapPx = 16f;
    private const float BonusGapPx = 46f;
    private const float BonusPopSeconds = 0.12f;
    private const float BonusHoldSeconds = 0.35f;
    private const float BonusFadeSeconds = 0.2f;
    private const float TintFadeDelay = 0.05f;
    private const float TintFadeSeconds = 0.3f;
    private const float JitterPx = 4f;
    private const float ShakePx = 2.5f;
    private const float FlashSeconds = 0.16f;
    private const float MiniPulse = 0.06f;            // 짧은 버전 완료 박동 (히트스톱·플래시 없이)
    private const float StepPulse = 0.035f;           // 상승 모드: 한 칸 오를 때 박동
    private const float DecimalNoiseChance = 0.4f;    // 상승 모드: 소수 자리가 글리치로 흔들릴 확률 (틱마다)
    private const float GhostAlpha = 0.55f;           // 레퍼런스는 하늘색 위주 — 분홍 잔상이 너무 튀지 않게

    // 해시 소금 (용도별로 다른 난수 줄기)
    private const int SaltCut = 1, SaltShift = 2, SaltDir = 3, SaltTint = 4, SaltTear = 5, SaltAlt = 6, SaltAltDigit = 7;
    private const int SaltDigit = 8, SaltInfect = 9, SaltScanCount = 10, SaltScan = 11, SaltBlock = 12;
    private const int SaltRgb = 13, SaltJitter = 14, SaltBar = 15, SaltFlicker = 16;

    /// <summary>해킹 한 번. 처치 기준 Start초에 시작하며, 이전 해킹들이 붙인 보너스(BaseAdded) 위에 Bonus를 더한다.</summary>
    internal struct Run
    {
        /// <summary>한 번만 터지는 이벤트 키 구분용 번호.</summary>
        public int Index;
        public float Start;
        public double BaseAdded;
        /// <summary>이 해킹이 붙이는 보너스 (합산이면 합계).</summary>
        public float Bonus;
        /// <summary>합쳐진 발동 수 — 2 이상이면 "×N" 표시.</summary>
        public int Stacks;
        /// <summary>합산으로 늘어난 고정 시작 지연 (초).</summary>
        public float LockDelay;
        /// <summary>짧은 버전: 뒤섞임·막대 없이 바로 새 값으로 튀고 가라앉는다.</summary>
        public bool Mini;
        /// <summary>앞 해킹 결과에 이어 붙는 짧은 버전 — 막대·ACCESS GRANTED를 유지하고 결과 글자 숫자만 올린다.</summary>
        public bool Chain;
        /// <summary>결과 글자에 쓰는 누적 보너스·발동 수 (0이면 Bonus·Stacks).</summary>
        public float DisplayBonus;
        public int DisplayStacks;
        /// <summary>마지막으로 합쳐진 순간(해킹 기준 초)과 그때 진행률 — 이후 남은 시간 동안 이어서 차므로 막대가 뒤로 가지 않는다.</summary>
        public float ProgressFromTime;
        public float ProgressFrom;
    }

    private sealed class Slice
    {
        public RectTransform Band;
        public TimerBonusDigits Digits;
    }

    private TimerBonusLab _lab;
    private RectTransform _root;
    private RectTransform _sliceRoot;
    private readonly Slice[] _slices = new Slice[MaxSlices];
    private readonly Image[] _scan = new Image[MaxScanlines];
    private readonly Image[] _blocks = new Image[MaxBlocks];
    private Image _barBack;
    private RectTransform _barFill;
    private Image _barFillImage;
    private TextMeshProUGUI _state;
    private TextMeshProUGUI _bonus;
    private readonly int[] _digits = new int[MaxReels];
    private readonly bool[] _scrambling = new bool[MaxReels];
    private readonly string[] _percentText = new string[101];
    private Color[] _lockColors;
    private Color[] _doneColors;
    private bool _slicesOn = true;
    private readonly bool _countUp;
    private object _owner;
    private Run _run;
    private float _textBonus = float.NaN;
    private int _textStacks;
    private string _runBonusText;

    /// <param name="countUp">true = 시안 F (숫자가 글리치하며 차오름), false = 시안 E (뒤섞였다가 왼쪽부터 고정)</param>
    public TimerBonusHack(bool countUp = false) => _countUp = countUp;

    /// <inheritdoc />
    public string Name => _countUp ? "F 해킹 상승" : "E 해킹";

    /// <inheritdoc />
    public void Setup(TimerBonusLab lab)
    {
        if (_lab != null) return;   // 중첩 시안 여럿이 한 렌더러를 같이 쓴다
        _lab = lab;
        var s = lab.Settings;
        float px = lab.Px;
        for (int i = 0; i < _percentText.Length; i++) _percentText[i] = $"TIME HACK  {i}%";
        _lockColors = new[] { s.CyanColor, Color.white };
        _doneColors = new[] { s.CyanColor, s.HotColor, Color.white };

        // 타이머 루트 아래 (본체보다 위): 띠 사본 → 스캔라인 → 노이즈 조각 순서로 겹친다.
        _root = TimerBonusLab.NewRect(_countUp ? "HackUp" : "Hack", lab.TimerRoot, Vector2.zero);
        _sliceRoot = TimerBonusLab.NewRect("Slices", _root, Vector2.zero);
        float bandWidth = lab.TimerSize.x * BandWidthScale;
        for (int i = 0; i < MaxSlices; i++)
        {
            var band = TimerBonusLab.NewRect((_countUp ? "UpSlice" : "Slice") + i, _sliceRoot, new Vector2(bandWidth, 10f));
            band.gameObject.AddComponent<RectMask2D>();
            var digits = new TimerBonusDigits(band, "Digits", lab.Font, lab.TimerFontSize, lab.TimerIntDigits);
            digits.MatchStyle(lab.TimerStyleSource);
            _slices[i] = new Slice { Band = band, Digits = digits };
        }
        var scanRoot = TimerBonusLab.NewRect("Scanlines", _root, Vector2.zero);
        for (int i = 0; i < MaxScanlines; i++) _scan[i] = TimerBonusLab.CreateImage("Scan" + i, scanRoot, Color.clear);
        var blockRoot = TimerBonusLab.NewRect("Noise", _root, Vector2.zero);
        for (int i = 0; i < MaxBlocks; i++) _blocks[i] = TimerBonusLab.CreateImage("Block" + i, blockRoot, Color.clear);

        _barBack = TimerBonusLab.CreateImage("HackBar", lab.FxLayer, new Color(s.CyanColor.r, s.CyanColor.g, s.CyanColor.b, 0.18f));
        _barBack.rectTransform.sizeDelta = new Vector2(BarWidthPx, BarHeightPx) * px;
        _barBack.rectTransform.anchoredPosition = new Vector2(lab.TimerPosition.x, BarY);
        _barFillImage = TimerBonusLab.CreateImage("Fill", _barBack.rectTransform, s.CyanColor);
        _barFill = _barFillImage.rectTransform;
        _barFill.anchorMin = new Vector2(0f, 0f);
        _barFill.anchorMax = new Vector2(0f, 1f);
        _barFill.pivot = new Vector2(0f, 0.5f);
        _barFill.anchoredPosition = Vector2.zero;
        _barFill.sizeDelta = Vector2.zero;

        _state = lab.CreateText("HackState", 13f, s.CyanColor);
        _state.characterSpacing = 8f;
        _bonus = lab.CreateText("HackBonus", 30f, s.CyanColor);
    }

    /// <inheritdoc />
    public void SetVisible(bool visible) => Show(this, visible);

    /// <summary>
    /// 렌더러를 같이 쓰는 시안들의 표시 — 마지막으로 켠 시안만 끌 수 있다
    /// (실험실이 시작 시 모든 시안에 SetVisible을 순서대로 부르므로, 뒤에 오는 false가 선택된 시안을 끄지 않게).
    /// </summary>
    internal void Show(object owner, bool visible)
    {
        if (visible) _owner = owner;
        else if (_owner != null && _owner != owner) return;
        else _owner = null;
        _root.gameObject.SetActive(visible);
        _barBack.gameObject.SetActive(visible);
        _state.gameObject.SetActive(visible);
        _bonus.gameObject.SetActive(visible);
    }

    /// <inheritdoc />
    public void Render(float a, float step)
        => RenderRun(a, new Run { Start = 0f, Bonus = _lab.Bonus, Stacks = 1 });

    /// <summary>해킹 시작부터 고정이 끝나는 순간까지 초 (처치 기준이 아닌 해킹 기준).</summary>
    internal float EndOf(in Run run)
    {
        Timings(run, out _, out _, out float end);
        return end;
    }

    /// <summary>해킹이 끝나고 글리치가 가라앉기까지 포함한 길이 (대기열 간격).</summary>
    internal float FinishOf(in Run run) => EndOf(run) + _lab.Settings.Hack.SettleSeconds;

    /// <summary>해킹 기준 초 — 결과 글자·막대·잔진동까지 모두 사라지는 순간 (인게임 연출 종료 판정).</summary>
    internal float VisibleUntilOf(in Run run)
    {
        var tune = _lab.Settings.Hack;
        float tail = Mathf.Max(tune.SettleSeconds, BonusPopSeconds + BonusHoldSeconds + BonusFadeSeconds, BarHoldSeconds + BarFadeSeconds);
        if (tune.AftershockTimes != null && !run.Mini)
            foreach (float t in tune.AftershockTimes) tail = Mathf.Max(tail, t + tune.AftershockSeconds);
        return EndOf(run) + tail;
    }

    private int ReelCount => _lab.MainDigits.ReelCount;

    private void Timings(in Run run, out float infectEnd, out float lockStart, out float end)
    {
        var tune = _lab.Settings.Hack;
        if (run.Mini) { infectEnd = lockStart = end = 0f; return; }
        infectEnd = tune.InfectSeconds;
        lockStart = infectEnd + tune.HackSeconds + Mathf.Max(0f, run.LockDelay);
        end = lockStart + (ReelCount - 1) * tune.LockStagger;
    }

    /// <summary>해킹 한 번을 처치 기준 absolute초 시점으로 그린다.</summary>
    internal void RenderRun(float absolute, in Run run)
    {
        _run = run;
        float a = absolute - run.Start;
        var s = _lab.Settings;
        var tune = s.Hack;
        float px = _lab.Px;
        Vector2 timer = _lab.TimerPosition;
        int k = Mathf.FloorToInt(a / Mathf.Max(0.005f, tune.TickSeconds));
        Timings(run, out float infectEnd, out float lockStart, out float end);
        float g = Intensity(a, k, tune, infectEnd, end, run.Mini);
        float progress = Progress(RawProgress(run, a), a >= end);

        if (_countUp) RenderCountUp(a, k, progress, infectEnd, end, timer, px);
        else RenderDigits(a, k, g, tune, infectEnd, lockStart, end);

        float tint = a < 0f ? 0f : a < end ? 1f : 1f - Mathf.Clamp01((a - end - TintFadeDelay) / TintFadeSeconds);
        _lab.SetHack(tint * tune.CyanTint);
        var baseColor = Color.Lerp(_lab.TimerBaseColor, s.CyanColor, tint * tune.CyanTint);
        if (a >= end) baseColor = Color.Lerp(baseColor, Color.white, 0.7f * (1f - Mathf.Clamp01((a - end) / FlashSeconds)));

        bool on = g > 0.001f;
        if (on != _slicesOn) { _slicesOn = on; _sliceRoot.gameObject.SetActive(on); }
        _lab.SetTimerAlpha(on ? 0f : 1f);
        if (on) LayoutSlices(k, g, tune, px, baseColor, a < end);
        LayoutScanlines(k, g, tune, px);
        LayoutBlocks(k, g, tune, px);

        float rgb = tune.RgbOffset * g * Mathf.Lerp(0.4f, 1f, Hash(k, 0, SaltRgb)) * (Hash(k, 1, SaltRgb) < 0.5f ? -1f : 1f);
        _lab.SetGhosts(rgb, on ? GhostAlpha : 0f);

        float tx = on ? (Hash(k, 0, SaltJitter) - 0.5f) * 2f * JitterPx * g : 0f;
        float squash = on ? 1f - 0.06f * g * Hash(k, 1, SaltJitter) : 1f;
        float sc = a >= end ? 1f + 0.16f * Spring(a - end, 22f, 7f) : 1f;
        _lab.TimerTransform(tx, 0f, sc, sc * squash, 0f);
        _lab.SetFlash(a >= end ? 1f - Mathf.Clamp01((a - end) / FlashSeconds) : 0f);
        if (on) _lab.AddShake((Hash(k, 2, SaltJitter) - 0.5f) * 2f * ShakePx * g, (Hash(k, 3, SaltJitter) - 0.5f) * 2f * ShakePx * g);

        RenderBar(a, k, g, progress, infectEnd, end, px);
        RenderBonus(a, k, end, px);

        // 자릿수 고정마다 짧게 튀고, 마지막에 크게 정상화
        for (int j = 0; j < ReelCount && !_countUp; j++)
        {
            if (run.Mini || a < lockStart + j * tune.LockStagger || !_lab.Once(Key(KeyLockBase + j))) continue;
            var digits = _lab.MainDigits;
            _lab.Burst(timer + digits.CellPosition(j), 6, 120f, 300f, _lockColors, life0: 0.15f, life1: 0.3f, size0: 1.2f, size1: 2.2f);
            _lab.Pulse += 0.04f;
        }
        if (a >= end && _lab.Once(Key(KeyDone)))
        {
            if (run.Mini)
            {
                _lab.Pulse += MiniPulse;
                _lab.Ring(timer, 20f, 80f, s.CyanColor, 0.28f);
                _lab.Burst(timer, 10, 160f, 380f, _doneColors, drag: 0.07f);
            }
            else
            {
                // 인게임은 타이머만 터지게 — 화면 전체 플래시·줌 없이 연출 시계만 잠깐 멈춘다.
                if (_lab.IsRuntime && !tune.RuntimeScreenFlash) _lab.HitStop(0.85f);
                else _lab.Impact(0.85f);
                _lab.Ring(timer, 30f, 125f, s.CyanColor, 0.4f);
                _lab.Burst(timer, 22, 220f, 560f, _doneColors, drag: 0.07f);
            }
        }
    }

    // 해킹마다 Once 키를 따로 쓴다 (실험실 공통 키 1·2·2000번대와 겹치지 않는 구간).
    private int Key(int local) => KeyRunBase + _run.Index * KeyRunStride + local;

    private double RunNow => _lab.Now + _run.BaseAdded;

    private string RunBonusText
    {
        get
        {
            float bonus = _run.DisplayStacks > 0 ? _run.DisplayBonus : _run.Bonus;
            int stacks = _run.DisplayStacks > 0 ? _run.DisplayStacks : _run.Stacks;
            if (bonus != _textBonus || stacks != _textStacks)
            {
                _textBonus = bonus;
                _textStacks = stacks;
                string b = "+" + bonus.ToString("0.00", CultureInfo.InvariantCulture);
                _runBonusText = stacks > 1 ? b + "  x" + stacks : b;
            }
            return _runBonusText;
        }
    }

    // 글리치 세기 0~1: 감염(깜빡) → 해킹(최대 근처에서 들쭉날쭉) → 완료 직후 최대에서 가라앉음 → 잔진동.
    private static float Intensity(float a, int k, TimerBonusLabSettings.HackTuning tune, float infectEnd, float end, bool mini)
    {
        if (a < 0f) return 0f;
        if (a < infectEnd) return (k & 1) == 0 ? 0.8f : 0f;
        if (a < end) return Mathf.Lerp(0.6f, 1f, Hash(k, 0, SaltFlicker));
        float g = 1f - Mathf.Clamp01((a - end) / Mathf.Max(0.01f, tune.SettleSeconds));
        var shocks = tune.AftershockTimes;
        if (shocks != null && !mini)
            foreach (float t in shocks)
                if (a >= end + t && a < end + t + tune.AftershockSeconds) g = Mathf.Max(g, 0.45f);
        return g;
    }

    // 감염 중엔 진짜 시간에 가끔 한 자리만 튐 → 해킹 중엔 전부 뒤섞임 → 왼쪽부터 목표값 고정 → 완료 후 진짜 시간 + 보너스.
    // 회전 중에도 실제 시간은 흐르므로 목표는 마지막 자리가 고정되는 순간의 값 (슬롯 릴과 같음).
    private void RenderDigits(float a, int k, float g, TimerBonusLabSettings.HackTuning tune, float infectEnd, float lockStart, float end)
    {
        for (int j = 0; j < ReelCount; j++) _scrambling[j] = false;
        var main = _lab.MainDigits;
        if (a < 0f || a >= end)
        {
            double v = RunNow + (a >= end ? _run.Bonus : 0f);
            _lab.SetTimerValue(v);
            string shown = main.FormatValue(v);
            SetLeadingHidden(!HasLeading(shown));
            Fill(shown);
            return;
        }
        string live = main.FormatValue(RunNow);
        string to = main.FormatValue(_lab.RemainingAt(_run.Start + end) + _run.BaseAdded + _run.Bonus);
        // 100초를 넘나들지 않으면 맨 앞(백의 자리) 칸은 숨긴 채 뒤섞는다.
        SetLeadingHidden(!HasLeading(live) && !HasLeading(to));
        for (int j = 0; j < ReelCount; j++)
        {
            int ci = main.CharIndexOf(j);
            int d;
            if (a < infectEnd)
                d = g > 0f && Hash(k, j, SaltInfect) < 0.35f ? Digit(k, j, SaltDigit) : live[ci] - '0';
            else if (a >= lockStart + j * tune.LockStagger) d = to[ci] - '0';
            else { d = Digit(k, j, SaltDigit); _scrambling[j] = true; }
            _digits[j] = d;
            _lab.SetTimerDigit(j, d);
        }
    }

    // 상승 모드: 원래 시간 + 보너스 × 진행률(막대와 같은 끊긴 단계). 소수 두 자리는 틱마다 글리치로 흔들리고,
    // 한 단계 오를 때마다 박동 + 위로 튀는 불꽃. 진행률 1(완료)에서 정확히 (지금 + 보너스)가 되어 이후 표시와 이어진다.
    private void RenderCountUp(float a, int k, float progress, float infectEnd, float end, Vector2 timer, float px)
    {
        for (int j = 0; j < ReelCount; j++) _scrambling[j] = false;
        double v = RunNow + (a >= infectEnd ? _run.Bonus * progress : 0f);
        _lab.SetTimerValue(v);
        string shown = _lab.MainDigits.FormatValue(v);
        SetLeadingHidden(!HasLeading(shown));
        Fill(shown);
        if (a < infectEnd || a >= end) return;

        for (int j = _lab.MainDigits.IntDigits; j < ReelCount; j++)
        {
            if (Hash(k, j, SaltInfect) >= DecimalNoiseChance) continue;
            _digits[j] = Digit(k, j, SaltDigit);
            _scrambling[j] = true;
            _lab.SetTimerDigit(j, _digits[j]);
        }
        int stepIndex = Mathf.RoundToInt(progress * BarSteps);
        if (stepIndex > 0 && _lab.Once(Key(KeyStepBase + stepIndex)))
        {
            _lab.Pulse += StepPulse;
            float hx = _lab.TimerSize.x * 0.5f;
            _lab.Burst(timer + new Vector2(TimerBonusLab.Rnd(-hx, hx), _lab.TimerSize.y * 0.35f), 5, 160f, 340f, _lockColors,
                life0: 0.18f, life1: 0.32f, size0: 1.2f, size1: 2.2f, angle: Mathf.PI * 0.5f, spread: 0.5f);
        }
    }

    /// <summary>
    /// 연속 진행률 0~1 (해킹 기준 a초). 합쳐진 적이 있으면 그 순간 값에서 남은 시간 동안 이어서 찬다 —
    /// 고정이 늦춰져도 막대가 뒤로 가지 않고 느려지기만 한다.
    /// </summary>
    internal float RawProgress(in Run run, float a)
    {
        Timings(run, out float infectEnd, out _, out float end);
        if (a < infectEnd) return 0f;
        if (a >= end) return 1f;
        if (run.ProgressFromTime > 0f)
            return Mathf.Clamp01(run.ProgressFrom + (1f - run.ProgressFrom) * (a - run.ProgressFromTime) / Mathf.Max(0.01f, end - run.ProgressFromTime));
        return Mathf.Clamp01((a - infectEnd) / Mathf.Max(0.01f, end - infectEnd));
    }

    /// <summary>해킹 기준 초 — 고정이 시작되는 순간 (이후 발동은 합치지 않고 이어 붙인다).</summary>
    internal float LockStartOf(in Run run)
    {
        Timings(run, out _, out float lockStart, out _);
        return lockStart;
    }

    // 진행률 → 막대·상승 숫자에 쓰는 끊긴 단계값. 단조 증가하는 울퉁불퉁한 곡선을 단계로 잘라
    // 칸마다 머무는 시간이 들쭉날쭉하되 절대 뒤로 가지 않는다 (숫자가 내려가 보이면 안 됨).
    private static float Progress(float raw, bool done)
    {
        if (done) return 1f;
        float w = raw + StutterAmount / (2f * Mathf.PI * StutterFrequency) * Mathf.Sin(2f * Mathf.PI * StutterFrequency * raw);
        return Mathf.Clamp01(Mathf.Floor(Mathf.Clamp01(w) * BarSteps) / BarSteps);
    }

    private void Fill(string formatted)
    {
        var main = _lab.MainDigits;
        for (int j = 0; j < ReelCount; j++) _digits[j] = formatted[main.CharIndexOf(j)] - '0';
    }

    // 정수 3자리 묶음에서 백의 자리가 있으면 true (2자리 실험실 묶음은 항상 false → 숨김 처리 자체가 없음).
    private bool HasLeading(string formatted) => _lab.MainDigits.IntDigits >= 3 && formatted[0] != '0';

    private void SetLeadingHidden(bool hidden)
    {
        _lab.SetTimerLeadingHidden(hidden);
        foreach (var sl in _slices) sl?.Digits.SetLeadingHidden(hidden);
    }

    // 숫자 칸 높이를 n개의 가로 띠로 나눠 각 띠에 마스크한 숫자 사본을 놓고, 띠마다 좌우로 어긋내고 물들인다.
    private void LayoutSlices(int k, float g, TimerBonusLabSettings.HackTuning tune, float px, Color baseColor, bool hacking)
    {
        var s = _lab.Settings;
        int n = Mathf.Clamp(tune.SliceCount, 2, MaxSlices);
        float h = _lab.TimerSize.y * SliceSpan;
        float bandWidth = _lab.TimerSize.x * BandWidthScale;
        float prev = -h * 0.5f;
        int tear = Mathf.Min(n - 1, (int)(Hash(k, 0, SaltTear) * n));
        for (int i = 0; i < MaxSlices; i++)
        {
            var sl = _slices[i];
            bool active = i < n;
            if (sl.Band.gameObject.activeSelf != active) sl.Band.gameObject.SetActive(active);
            if (!active) continue;

            float top = i == n - 1 ? h * 0.5f : -h * 0.5f + h * (i + 1 + (Hash(k, i, SaltCut) - 0.5f) * CutJitter) / n;
            top = Mathf.Max(top, prev);
            float center = (prev + top) * 0.5f;
            sl.Band.anchoredPosition = new Vector2(0f, center);
            sl.Band.sizeDelta = new Vector2(bandWidth, top - prev + SliceOverlap);
            prev = top;

            float dx = 0f;
            if (Hash(k, i, SaltShift) < 0.3f + 0.45f * g)
                dx = (Hash(k, i, SaltDir) * 2f - 1f) * tune.MaxSliceShift * px * g;
            if (i == tear && g > 0.5f) dx *= 2f;
            sl.Digits.Root.anchoredPosition = new Vector2(dx, -center);

            float r = Hash(k, i, SaltTint);
            var c = r < 0.22f * g ? s.CyanColor : r < 0.27f * g ? s.HotColor : r < 0.34f * g ? Color.white : baseColor;
            sl.Digits.SetColor(c);

            bool alt = hacking && Hash(k, i, SaltAlt) < AltDigitChance * g;
            for (int j = 0; j < ReelCount; j++)
                sl.Digits.SetDigit(j, alt && _scrambling[j] ? Digit(k, i * ReelCount + j, SaltAltDigit) : _digits[j]);
        }
    }

    // 레퍼런스의 하늘색 가로줄: 대부분 숫자 폭 안에서 튀고, 일부는 화면을 가로지르는 옅은 찢김선.
    private void LayoutScanlines(int k, float g, TimerBonusLabSettings.HackTuning tune, float px)
    {
        var s = _lab.Settings;
        Vector2 size = _lab.TimerSize;
        int m = Mathf.Min(tune.ScanlineCount, MaxScanlines);
        int count = Mathf.RoundToInt(m * g * (0.5f + 0.5f * Hash(k, 0, SaltScanCount)));
        for (int i = 0; i < MaxScanlines; i++)
        {
            var img = _scan[i];
            if (i >= count) { TimerBonusLab.Hide(img); continue; }
            int salt = SaltScan + i * 8;
            bool wide = Hash(k, salt, 0) < 0.25f;
            float w = wide ? _lab.StageSize.x * 0.9f : size.x * Mathf.Lerp(0.3f, 1.1f, Hash(k, salt, 1));
            float x = wide ? 0f : (Hash(k, salt, 2) - 0.5f) * size.x * 0.5f;
            float y = (Hash(k, salt, 3) - 0.5f) * size.y * SliceSpan;
            float thick = Mathf.Lerp(1.5f, 4.5f, Hash(k, salt, 4)) * px;
            img.color = Hash(k, salt, 5) < 0.7f ? s.CyanColor : Color.white;
            img.rectTransform.sizeDelta = new Vector2(w, thick);
            TimerBonusLab.Place(img, new Vector2(x, y), Vector2.one, wide ? 0.35f : Mathf.Lerp(0.6f, 1f, Hash(k, salt, 6)));
        }
    }

    // 숫자 둘레에 흩어지는 작은 픽셀 조각.
    private void LayoutBlocks(int k, float g, TimerBonusLabSettings.HackTuning tune, float px)
    {
        var s = _lab.Settings;
        Vector2 size = _lab.TimerSize;
        int count = Mathf.RoundToInt(Mathf.Min(tune.NoiseBlockCount, MaxBlocks) * g);
        for (int i = 0; i < MaxBlocks; i++)
        {
            var img = _blocks[i];
            if (i >= count) { TimerBonusLab.Hide(img); continue; }
            int salt = SaltBlock + i * 8;
            float r = Hash(k, salt, 0);
            img.color = r < 0.6f ? s.CyanColor : r < 0.8f ? Color.white : s.HotColor;
            img.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(6f, 28f, Hash(k, salt, 1)), Mathf.Lerp(2f, 8f, Hash(k, salt, 2))) * px;
            var pos = new Vector2((Hash(k, salt, 3) - 0.5f) * size.x * 1.1f, (Hash(k, salt, 4) - 0.5f) * size.y * 1.2f);
            TimerBonusLab.Place(img, pos, Vector2.one, Mathf.Lerp(0.5f, 0.9f, Hash(k, salt, 5)));
        }
    }

    // 타이머 아래 진행 막대: 끊기며 차오르고 가끔 깜빡, 다 차면 하얗게 번쩍 → 사라짐.
    private void RenderBar(float a, int k, float g, float p, float infectEnd, float end, float px)
    {
        float alpha = (_run.Mini && !_run.Chain) || a < infectEnd ? 0f : a < end + BarHoldSeconds ? 1f : 1f - Mathf.Clamp01((a - end - BarHoldSeconds) / BarFadeSeconds);
        if (a < end && Hash(k, 0, SaltBar) < 0.12f * g) alpha *= 0.3f;
        _barBack.canvasRenderer.SetAlpha(alpha);
        _barFillImage.canvasRenderer.SetAlpha(alpha);
        _barBack.rectTransform.anchoredPosition = new Vector2(_lab.TimerPosition.x, BarY);
        _barFill.sizeDelta = new Vector2(p * BarWidthPx * px, 0f);
        float flash = a >= end ? 1f - Mathf.Clamp01((a - end) / FlashSeconds) : 0f;
        _barFillImage.color = Color.Lerp(_lab.Settings.CyanColor, Color.white, flash);

        TimerBonusLab.SetText(_state, a < end ? _percentText[Mathf.RoundToInt(p * 100f)] : "ACCESS GRANTED");
        TimerBonusLab.Place(_state, new Vector2(_lab.TimerPosition.x, BarY - StateGapPx * px), Vector2.one, alpha);
    }

    // 완료 순간 "+15.00"이 글리치로 튀어나왔다가 사라진다.
    private void RenderBonus(float a, int k, float end, float px)
    {
        float t = a - end;
        if (t < 0f || t > BonusPopSeconds + BonusHoldSeconds + BonusFadeSeconds) { TimerBonusLab.Hide(_bonus); return; }
        TimerBonusLab.SetText(_bonus, RunBonusText);
        float pop = _run.Chain
            ? 1f + ChainPunch * (1f - OutCubic(Mathf.Clamp01(t / BonusPopSeconds)))
            : OutBack(Mathf.Clamp01(t / BonusPopSeconds), 2.2f);
        float alpha = 1f - Mathf.Clamp01((t - BonusPopSeconds - BonusHoldSeconds) / BonusFadeSeconds);
        float jx = 0f;
        if (t < BonusPopSeconds * 1.5f)
        {
            jx = (Hash(k, 0, SaltFlicker + 100) - 0.5f) * 2f * JitterPx * 2f * px;
            if (Hash(k, 1, SaltFlicker + 100) < 0.3f) alpha *= 0.35f;
        }
        TimerBonusLab.Place(_bonus, new Vector2(_lab.TimerPosition.x + jx, BarY - BonusGapPx * px), new Vector2(pop, pop), alpha);
    }

    private float BarY => _lab.TimerPosition.y - _lab.TimerSize.y * 0.5f
        - (BarGapPx + (_lab.IsRuntime ? _lab.Settings.Hack.RuntimeBarDropPx : 0f)) * _lab.Px;

    private static int Digit(int k, int i, int salt) => Mathf.Min(9, (int)(Hash(k, i, salt) * 10f));

    // 틱·번호·소금 → 0~1 (같은 입력에는 항상 같은 값)
    private static float Hash(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFFu) / 16777216f;
        }
    }
}
