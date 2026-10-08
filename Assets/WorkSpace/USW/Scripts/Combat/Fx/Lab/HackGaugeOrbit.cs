using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스택 생성 때 HUD 자리에서 조립되고, 소진 때 접혀 사라지는 독립 링.</summary>
public sealed class HackGaugeOrbit : HackStackGauge
{
    /// <summary>실험실에서 비교할 충전 띠 형태.</summary>
    public enum Look { Segmented, Smooth, OpenArc }

    private const float Diameter = 208f;
    private const float EchoDuration = .85f;
    private const float EnterDuration = .7f;
    private const float ExitDuration = .4f;
    private enum Phase { Hidden, Entering, Visible, Exiting }
    private static readonly Vector2 Center = new Vector2(.5f, .5f);
    private readonly Look _look;
    private readonly Image _fill, _echo, _wave, _track;
    private readonly HackTemporalTransition _transition;
    private readonly CanvasGroup _visibility;
    private readonly TextMeshProUGUI _count, _capacity, _state;
    private float _consumeAt = -10f, _echoRatio;
    private int _consumed, _lastCount = -1, _lastMax = -1;
    private Phase _phase;
    private float _phaseAt;
    private int _shownCount;

    /// <inheritdoc/>
    public override string Name => _look == Look.Segmented ? "분할 링" : _look == Look.Smooth ? "스캔 링" : "오픈 링";
    /// <inheritdoc/>
    protected override bool ShowEmpty => true;
    /// <inheritdoc/>
    protected override float PulseScale => .025f;
    /// <inheritdoc/>
    protected override float HudScale => 1f;

    /// <inheritdoc/>
    public override bool Active
    {
        set { base.Active = value; if (!value) _transition.Hide(); }
    }

    /// <summary>HP바 우측 하단에 놓을 독립 계기판을 만든다.</summary>
    public HackGaugeOrbit(RectTransform parent, TMP_FontAsset font, Material material, Look look)
        : base(parent, font, material, Vector2.one * Diameter, Center)
    {
        _look = look;
        Root.name = "HackOrbit_" + look;
        _visibility = Root.GetComponent<CanvasGroup>();
        _transition = new HackTemporalTransition(parent, "HackTemporal_" + look);
        Box("Outline", Root, Center, Vector2.zero, Vector2.one * Diameter, new Color(.025f, .04f, .07f), HackGaugeSprites.Disc);
        Box("Face", Root, Center, Vector2.zero, Vector2.one * (Diameter - 10f), new Color(.045f, .085f, .15f), HackGaugeSprites.Disc);
        Sprite ring = look == Look.Segmented ? HackGaugeSprites.ChargeRing : HackGaugeSprites.Ring;
        _track = Ring("Track", Diameter - 10f, Track, ring);
        _echo = Ring("SpentEcho", Diameter - 10f, Color.clear, ring);
        _fill = Ring("Charge", Diameter - 10f, Color.cyan, ring);
        SetArc(_track, 1f);
        _wave = Ring("DischargeWave", Diameter, Color.clear, HackGaugeSprites.ThinRing);
        _wave.fillAmount = 1f;
        var title = Text("Title", Root, Center, new Vector2(0f, 43f), new Vector2(116f, 26f), 21f, TextAlignmentOptions.Center);
        title.text = "HACK"; title.fontStyle = FontStyles.Bold;
        title.color = new Color(.5f, .85f, 1f);
        _count = Text("Stacks", Root, Center, new Vector2(0f, 3f), new Vector2(136f, 65f), 57f, TextAlignmentOptions.Center);
        _count.fontStyle = FontStyles.Bold;
        _capacity = Text("Capacity", Root, Center, new Vector2(0f, -36f), new Vector2(110f, 27f), 23f, TextAlignmentOptions.Center);
        _capacity.fontStyle = FontStyles.Bold; _capacity.color = new Color(.65f, .75f, .87f);
        _state = Text("State", Root, Center, new Vector2(0f, -Diameter * .5f - 17f), new Vector2(190f, 34f), 27f, TextAlignmentOptions.Center);
        _state.fontStyle = FontStyles.Bold;
    }

    /// <inheritdoc/>
    public override void Reset(int value, int max)
    {
        base.Reset(value, max);
        _consumeAt = -10f; _echoRatio = 0f; _consumed = 0;
        _phase = Phase.Hidden; _phaseAt = Time.time;
        _visibility.alpha = 0f;
        _transition.Hide();
        _state.text = "";
        if (Target > 0) BeginEnter();
    }

    /// <inheritdoc/>
    public override void Set(int value)
    {
        base.Set(value);
        if (Target > 0 && (_phase == Phase.Hidden || _phase == Phase.Exiting)) BeginEnter();
    }

    private void BeginEnter()
    {
        _phase = Phase.Entering; _phaseAt = Time.time;
        _consumeAt = -10f; _consumed = 0; _echoRatio = 0f;
    }

    /// <inheritdoc/>
    public override void Tick(float now, Vector2 bossLocal)
    {
        base.Tick(now, bossLocal);
        if (Target == 0 && _shownCount == 0 && (_phase == Phase.Visible || _phase == Phase.Entering))
        {
            _phase = Phase.Exiting; _phaseAt = now;
        }
        float duration = _phase == Phase.Exiting ? ExitDuration : EnterDuration;
        float q = Mathf.Clamp01((now - _phaseAt) / duration);
        float visible = 0f;
        if (_phase == Phase.Entering)
        {
            // 원호/눈금이 먼저 전개된 다음 실제 숫자 HUD가 자리 잡는다.
            visible = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((q - .52f) / .48f));
            _transition.Render(Root.anchoredPosition, q, false);
            Root.localScale *= Mathf.Lerp(.8f, 1f, visible);
            if (q >= 1f) _phase = Phase.Visible;
        }
        else if (_phase == Phase.Exiting)
        {
            visible = 1f - Mathf.SmoothStep(0f, 1f, q);
            Root.localScale *= Mathf.Lerp(1f, .45f, q);
            _transition.Render(Root.anchoredPosition, q, true);
            if (q >= 1f) _phase = Phase.Hidden;
        }
        else
        {
            visible = _phase == Phase.Visible ? 1f : 0f;
            _transition.Hide();
        }
        _visibility.alpha = visible;
        SetArc(_track, 1f);
    }

    /// <inheritdoc/>
    public override void Consume(int amount)
    {
        if (amount <= 0) return;
        base.Consume(amount);
        _consumed = amount; _consumeAt = Time.time;
        _echoRatio = Target / (float)Max;
        _state.text = "-" + amount;
    }

    /// <inheritdoc/>
    protected override Vector2 HudAnchor(Rect bar)
        => new Vector2(bar.xMax - Diameter * .5f - 16f, bar.yMin - Diameter * .5f - 24f);

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        _shownCount = shown;
        Color color = TierColor(ratio, now, ignite);
        float elapsed = now - _consumeAt;
        float echo = 1f - Mathf.Clamp01(elapsed / EchoDuration);
        SetArc(_fill, ratio);
        _fill.color = Color.Lerp(color, Color.white, bump * .25f);
        SetArc(_echo, Mathf.Max(ratio, _echoRatio));
        _echo.color = new Color(1f, .82f, .98f, echo * .8f);
        float q = Mathf.Clamp01(elapsed / EchoDuration);
        _wave.rectTransform.localScale = Vector3.one * (1f + .18f * q);
        _wave.color = new Color(color.r, color.g, color.b, echo * echo * .8f);
        if (_lastCount != shown) { _lastCount = shown; _count.text = shown.ToString(); }
        if (_lastMax != Max) { _lastMax = Max; _capacity.text = "/" + Max; }
        _count.color = Color.Lerp(Color.white, color, ignite * .4f);
        bool spending = _consumed > 0 && elapsed < EchoDuration;
        string state = spending ? "-" + _consumed : ratio >= 1f ? "MAX" : "";
        if (_state.text != state) _state.text = state;
        _state.color = spending ? new Color(1f, .85f, 1f, echo) : color;
        _state.rectTransform.anchoredPosition = new Vector2(0f, -Diameter * .5f - 17f - (spending ? q * 10f : 0f));
    }

    private Image Ring(string name, float size, Color color, Sprite sprite)
    {
        var image = Box(name, Root, Center, Vector2.zero, Vector2.one * size, color, sprite);
        image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top; image.fillClockwise = true;
        return image;
    }

    private void SetArc(Image image, float ratio)
    {
        image.fillAmount = Mathf.Clamp01(ratio) * (_look == Look.OpenArc ? .75f : 1f);
        image.rectTransform.localEulerAngles = new Vector3(0f, 0f, _look == Look.OpenArc ? -45f : 0f);
    }
}
