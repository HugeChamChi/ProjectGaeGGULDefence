using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스택 UI 링 시안 — 보스 HP바 왼쪽 해골 초상화(Boss_Icon)를 기준으로 그린다. HP바 아래 디버프 줄 자리는 건드리지 않는다.
/// 링1 얇은 링 / 링2 10칸 링 / 링3 왼쪽 호 / 링4 초상화 발광. 숫자는 초상화 아래 가운데 알약(디버프 첫 칸과 떨어진 자리).
/// </summary>
public sealed class HackGaugeSkullRing : HackStackGauge
{
    /// <summary>링 모양.</summary>
    public enum Look { Thin, Segmented, LeftArc, PortraitGlow }

    // 인게임 Boss_Icon 102px를 바깥에서 살짝 감싸는 크기 (링 띠 반지름 약 52~61)
    private const float Diameter = 124f;
    // HP바 왼쪽 끝 기준 해골 초상화 중심 — IngameScene 배치(Boss_Icon은 HP바 왼쪽 끝에서 20 왼쪽)
    private static readonly Vector2 SkullOffset = new Vector2(-20f, 0f);
    private static readonly Vector2 PillSize = new Vector2(54f, 26f);
    private const float ArcShare = .5f;
    private static readonly string[] Names = { "링1 얇은 링", "링2 10칸 링", "링3 왼쪽 호", "링4 초상화 발광" };
    private readonly Look _look;
    private readonly Image _glow, _fill, _pill;
    private readonly TextMeshProUGUI _count;

    /// <inheritdoc/>
    public override string Name => Names[(int)_look];

    /// <summary>링 시안 하나를 만든다. 발광은 backLayer(HUD보다 뒤)에 그려 초상화를 덮지 않는다.</summary>
    public HackGaugeSkullRing(RectTransform parent, RectTransform backLayer, TMP_FontAsset font, Material fontMaterial, Look look)
        : base(parent, font, fontMaterial, new Vector2(Diameter, Diameter), new Vector2(.5f, .5f))
    {
        _look = look;
        var center = new Vector2(.5f, .5f);
        _glow = Box("Glow_" + look, backLayer != null ? backLayer : Root, center, Vector2.zero, Vector2.one * (Diameter * 1.3f), Color.clear, HackGaugeSprites.SoftDisc);
        if (look != Look.PortraitGlow)
        {
            var ring = look == Look.Segmented ? HackGaugeSprites.SegmentRing : HackGaugeSprites.ThinRing;
            var track = Box("Track", Root, center, Vector2.zero, Vector2.one * Diameter, Track, ring);
            _fill = Box("Fill", Root, center, Vector2.zero, Vector2.one * Diameter, Color.white, ring);
            Radial(_fill, look);
            if (look == Look.LeftArc) { Radial(track, look); track.fillAmount = ArcShare; }
        }
        _pill = Box("Pill", Root, center, new Vector2(0f, -Diameter * .5f + 4f), PillSize, Back);
        _count = Text("Count", _pill.transform, center, Vector2.zero, PillSize, 20f, TextAlignmentOptions.Center);
    }

    /// <inheritdoc/>
    public override bool Active { set { base.Active = value; _glow.gameObject.SetActive(value); } }

    /// <inheritdoc/>
    protected override float HudScale => 1f;

    /// <inheritdoc/>
    protected override Vector2 HudAnchor(Rect bar) => new Vector2(bar.xMin + SkullOffset.x, bar.center.y + SkullOffset.y);

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        Color c = TierColor(ratio, now, ignite);
        bool max = ratio >= 1f;
        // 발광은 다른 레이어에 있으므로 위치·크기를 링 본체에 맞춘다 (같은 크기의 전체 화면 부모라 좌표가 같다).
        if (_glow.transform.parent != Root)
        {
            _glow.rectTransform.anchoredPosition = Root.anchoredPosition;
            _glow.rectTransform.localScale = Root.localScale;
        }
        float pulse = max ? .5f + .5f * Mathf.Sin(now * 8f) : 0f;
        if (_fill != null)
        {
            _fill.fillAmount = _look == Look.LeftArc ? ratio * ArcShare : ratio;
            _fill.color = Color.Lerp(c, Color.white, bump * .6f);
            _glow.color = new Color(c.r, c.g, c.b, .22f * bump + .35f * ignite + .18f * pulse);
        }
        else
        {
            // 초상화 뒤 빛이 스택만큼 커지고 진해진다.
            // 밝은 하늘 배경에서도 보이도록 초상화(102)보다 확실히 크게, 진하게.
            float size = Diameter * (1.05f + .75f * ratio + .1f * bump);
            _glow.rectTransform.sizeDelta = Vector2.one * size;
            _glow.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(.35f + .65f * ratio + .3f * bump + .3f * ignite) * (1f - .25f * pulse));
        }
        string count = shown.ToString();
        if (_count.text != count) _count.text = count;
        _count.color = max ? c : Color.Lerp(Color.white, c, bump * .6f);
    }

    // 링1·링2는 12시에서 시계 방향, 링3은 6시에서 시계 방향(왼쪽 반원)으로 찬다.
    private static void Radial(Image image, Look look)
    {
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)(look == Look.LeftArc ? Image.Origin360.Bottom : Image.Origin360.Top);
        image.fillClockwise = true;
    }
}
