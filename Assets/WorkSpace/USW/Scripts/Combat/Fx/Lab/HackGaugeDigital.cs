using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스택 UI 시안 E — 해커 단말 느낌의 고정폭 숫자 "HACK 047/100" + 얇은 막대. 값이 바뀌면 청록·자홍 잔상이 지직인다.</summary>
public sealed class HackGaugeDigital : HackStackGauge
{
    private const float BarWidth = 290f;
    private static readonly Color GhostCyan = new Color(0f, 1f, 1f, .7f);
    private static readonly Color GhostMagenta = new Color(1f, .2f, .8f, .7f);
    private readonly TextMeshProUGUI _main, _ghostA, _ghostB, _percent;
    private readonly Image _fill;

    /// <inheritdoc/>
    public override string Name => "E 디지털";

    /// <summary>보스 옆에 붙는 디지털 카운터를 만든다.</summary>
    public HackGaugeDigital(RectTransform parent, TMP_FontAsset font, Material fontMaterial)
        : base(parent, font, fontMaterial, new Vector2(310f, 74f), new Vector2(0f, .5f))
    {
        Box("Back", Root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(310f, 74f), new Color(Back.r, Back.g, Back.b, .6f));
        _ghostA = Text("GhostA", Root, new Vector2(0f, 1f), new Vector2(10f, -2f), new Vector2(300f, 50f), 38f, TextAlignmentOptions.Left);
        _ghostB = Text("GhostB", Root, new Vector2(0f, 1f), new Vector2(10f, -2f), new Vector2(300f, 50f), 38f, TextAlignmentOptions.Left);
        _main = Text("Main", Root, new Vector2(0f, 1f), new Vector2(10f, -2f), new Vector2(300f, 50f), 38f, TextAlignmentOptions.Left);
        _percent = Text("Percent", Root, new Vector2(1f, 0f), new Vector2(-10f, 12f), new Vector2(80f, 24f), 18f, TextAlignmentOptions.Right);
        Box("Track", Root, Vector2.zero, new Vector2(10f, 6f), new Vector2(BarWidth, 5f), Track);
        _fill = Box("Fill", Root, Vector2.zero, new Vector2(10f, 6f), new Vector2(BarWidth, 5f), Color.white);
    }

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        Color c = TierColor(ratio, now, ignite);
        string digits = shown.ToString(Max >= 100 ? "000" : "00");
        string text = ratio >= 1f && Mathf.Repeat(now * 4f, 1f) < .5f
            ? "HACK <mspace=0.62em>MAX</mspace>"
            : "HACK <mspace=0.62em>" + digits + "</mspace><size=60%>/" + Max + "</size>";
        if (_main.text != text) { _main.text = text; _ghostA.text = text; _ghostB.text = text; }
        _main.color = Color.Lerp(Color.white, c, .35f + .4f * bump);
        // 값이 바뀐 직후와 기폭 예고 중에만 RGB 잔상이 좌우로 벌어진다.
        float glitch = Mathf.Max(bump, ignite * (Mathf.Repeat(now * 20f, 1f) < .5f ? 1f : .3f));
        _ghostA.enabled = _ghostB.enabled = glitch > .05f;
        _ghostA.color = GhostCyan; _ghostB.color = GhostMagenta;
        _ghostA.rectTransform.anchoredPosition = new Vector2(10f - 5f * glitch, -2f);
        _ghostB.rectTransform.anchoredPosition = new Vector2(10f + 5f * glitch, -2f);
        _fill.color = c;
        Fill(_fill, BarWidth, ratio);
        string percent = Mathf.RoundToInt(ratio * 100f) + "%";
        if (_percent.text != percent) _percent.text = percent;
        _percent.color = c;
    }
}
