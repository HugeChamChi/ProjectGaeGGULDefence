using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스택 UI 시안 B — 원형 링이 시계 방향으로 차오른다.
/// 보스 옆: 링 가운데에 숫자. HUD: HP바 왼쪽 해골 아이콘을 링이 감싸고 숫자는 아이콘 아래 작은 딱지로.
/// </summary>
public sealed class HackGaugeRing : HackStackGauge
{
    private const float Diameter = 124f;
    // HP바 왼쪽 끝 기준 해골 아이콘(Boss_Icon) 중심 — 인게임 IngameScene 배치와 같은 값
    private static readonly Vector2 SkullOffset = new Vector2(-20f, 0f);
    private readonly Image _glow, _back, _fill, _badge;
    private readonly TextMeshProUGUI _count, _label;

    /// <inheritdoc/>
    public override string Name => "B 링";

    /// <summary>링을 만든다.</summary>
    public HackGaugeRing(RectTransform parent, TMP_FontAsset font, Material fontMaterial)
        : base(parent, font, fontMaterial, new Vector2(Diameter, Diameter), new Vector2(.5f, .5f))
    {
        var center = new Vector2(.5f, .5f);
        _glow = Box("Glow", Root, center, Vector2.zero, Vector2.one * (Diameter * 1.35f), Color.clear, HackGaugeSprites.Disc);
        _back = Box("Back", Root, center, Vector2.zero, Vector2.one * Diameter, Back, HackGaugeSprites.Disc);
        Box("Track", Root, center, Vector2.zero, Vector2.one * Diameter, Track, HackGaugeSprites.Ring);
        _fill = Box("Fill", Root, center, Vector2.zero, Vector2.one * Diameter, Color.white, HackGaugeSprites.Ring);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Radial360;
        _fill.fillOrigin = (int)Image.Origin360.Top;
        _fill.fillClockwise = true;
        _badge = Box("Badge", Root, center, new Vector2(0f, -Diameter * .5f - 6f), new Vector2(64f, 30f), Back);
        _count = Text("Count", Root, center, new Vector2(0f, 6f), new Vector2(Diameter, 50f), 36f, TextAlignmentOptions.Center);
        _label = Text("Label", Root, center, new Vector2(0f, -22f), new Vector2(Diameter, 22f), 16f, TextAlignmentOptions.Center);
    }

    /// <inheritdoc/>
    protected override Vector2 Anchor(Vector2 bossLocal) => bossLocal + new Vector2(Diameter * .5f, 0f);

    /// <inheritdoc/>
    protected override float HudScale => 1.15f;

    /// <inheritdoc/>
    protected override Vector2 HudAnchor(Rect bar) => new Vector2(bar.xMin + SkullOffset.x, bar.center.y + SkullOffset.y);

    /// <inheritdoc/>
    protected override void OnPlacement(bool hud)
    {
        // HUD에서는 해골 아이콘이 보이도록 가운데를 비우고 숫자를 아래 딱지로 옮긴다.
        _back.enabled = !hud;
        _label.enabled = !hud;
        _badge.enabled = hud;
        _count.rectTransform.anchoredPosition = hud ? _badge.rectTransform.anchoredPosition : new Vector2(0f, 6f);
        _count.fontSize = hud ? 22f : 36f;
    }

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        Color c = TierColor(ratio, now, ignite);
        _fill.fillAmount = ratio;
        _fill.color = Color.Lerp(c, Color.white, bump * .6f);
        _glow.color = new Color(c.r, c.g, c.b, .25f * bump + .35f * ignite + (ratio >= 1f ? .2f : 0f));
        string count = shown.ToString();
        if (_count.text != count) _count.text = count;
        _count.color = Color.Lerp(Color.white, c, bump * .5f);
        string label = ratio >= 1f ? "MAX" : "HACK";
        if (_label.text != label) _label.text = label;
        _label.color = c;
    }
}
