using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스택 UI 시안 A — 기존 박스를 발전: HACK 글자 + "47/100" + 25·50·75% 눈금이 있는 채움 막대.</summary>
public sealed class HackGaugeBox : HackStackGauge
{
    private const float BarWidth = 198f;
    private readonly Image _back, _accent, _fill;
    private readonly TextMeshProUGUI _label, _count;

    /// <inheritdoc/>
    public override string Name => "A 박스";

    /// <summary>보스 옆에 붙는 박스를 만든다.</summary>
    public HackGaugeBox(RectTransform parent, TMP_FontAsset font, Material fontMaterial)
        : base(parent, font, fontMaterial, new Vector2(230f, 88f), new Vector2(0f, .5f))
    {
        _back = Box("Back", Root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(230f, 88f), Back);
        _accent = Box("Accent", Root, new Vector2(0f, .5f), Vector2.zero, new Vector2(6f, 88f), Color.white);
        _label = Text("Label", Root, new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(130f, 28f), 22f, TextAlignmentOptions.TopLeft);
        _count = Text("Count", Root, new Vector2(1f, 1f), new Vector2(-12f, -2f), new Vector2(170f, 54f), 44f, TextAlignmentOptions.Right);
        Box("Track", Root, Vector2.zero, new Vector2(16f, 10f), new Vector2(BarWidth, 10f), Track);
        _fill = Box("Fill", Root, Vector2.zero, new Vector2(16f, 10f), new Vector2(BarWidth, 10f), Color.white);
        for (int i = 1; i <= 3; i++)
            Box("Tick" + i, Root, Vector2.zero, new Vector2(16f + BarWidth * i / 4f - 1f, 8f), new Vector2(2f, 14f), new Color(1f, 1f, 1f, .45f));
    }

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        Color c = TierColor(ratio, now, ignite);
        _accent.color = c;
        _fill.color = Color.Lerp(c, Color.white, bump * .6f);
        Fill(_fill, BarWidth, ratio);
        string label = ratio >= 1f ? "HACK MAX" : "HACK";
        if (_label.text != label) _label.text = label;
        _label.color = c;
        string count = Fraction(shown);
        if (_count.text != count) _count.text = count;
        _count.color = Color.Lerp(Color.white, c, bump * .5f);
        _back.color = Color.Lerp(Back, new Color(c.r, c.g, c.b, .9f), ignite * .5f);
    }
}
