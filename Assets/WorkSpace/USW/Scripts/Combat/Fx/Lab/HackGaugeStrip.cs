using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스택 UI 시안 F — HP바 바로 아래 얇은 해킹 줄(보조 게이지). HP바와 같은 폭이라 보스가 커도 작아도 자리가 그대로다.
/// 인게임에 넣으면 기존 디버프 줄을 이 줄 높이만큼 아래로 내려야 한다.
/// </summary>
public sealed class HackGaugeStrip : HackStackGauge
{
    private const float Height = 34f;
    private const float BarHeight = 10f;
    // HP바 프리팹 안쪽 막대 시작·끝 여백 (해골 아이콘 오른쪽부터)
    private const float InsetLeft = 40f, InsetRight = 20f;
    private readonly Image _track, _fill;
    private readonly TextMeshProUGUI _label, _count;
    private readonly Image[] _ticks = new Image[3];
    private float _width;

    /// <inheritdoc/>
    public override string Name => "F HP바 줄";

    /// <summary>얇은 해킹 줄을 만든다. 폭은 HP바에 맞춰 매 프레임 다시 잡는다.</summary>
    public HackGaugeStrip(RectTransform parent, TMP_FontAsset font, Material fontMaterial)
        : base(parent, font, fontMaterial, new Vector2(600f, Height), new Vector2(0f, 1f))
    {
        _track = Box("Track", Root, new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(600f, BarHeight), Track);
        _fill = Box("Fill", Root, new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(600f, BarHeight), Color.white);
        for (int i = 0; i < _ticks.Length; i++)
            _ticks[i] = Box("Tick" + i, Root, new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, BarHeight + 4f), new Color(1f, 1f, 1f, .45f));
        _label = Text("Label", Root, new Vector2(0f, 1f), new Vector2(0f, -14f), new Vector2(160f, 22f), 18f, TextAlignmentOptions.Left);
        _count = Text("Count", Root, new Vector2(1f, 1f), new Vector2(0f, -12f), new Vector2(160f, 26f), 22f, TextAlignmentOptions.Right);
    }

    /// <inheritdoc/>
    protected override float HudScale => 1f;

    /// <inheritdoc/>
    protected override Vector2 HudAnchor(Rect bar)
    {
        Resize(bar.width - InsetLeft - InsetRight);
        return new Vector2(bar.xMin + InsetLeft, bar.yMin + 2f);
    }

    /// <inheritdoc/>
    protected override Vector2 Anchor(Vector2 bossLocal)
    {
        Resize(420f);
        return bossLocal;
    }

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        Color c = TierColor(ratio, now, ignite);
        _fill.color = Color.Lerp(c, Color.white, bump * .6f);
        Fill(_fill, _width, ratio);
        string label = ratio >= 1f ? "HACK MAX" : "HACK";
        if (_label.text != label) _label.text = label;
        _label.color = c;
        string count = Fraction(shown);
        if (_count.text != count) _count.text = count;
        _count.color = Color.Lerp(Color.white, c, bump * .5f);
    }

    private void Resize(float width)
    {
        width = Mathf.Max(100f, width);
        if (Mathf.Approximately(width, _width)) return;
        _width = width;
        Root.sizeDelta = new Vector2(width, Height);
        var size = _track.rectTransform.sizeDelta; size.x = width; _track.rectTransform.sizeDelta = size;
        for (int i = 0; i < _ticks.Length; i++)
            _ticks[i].rectTransform.anchoredPosition = new Vector2(width * (i + 1) / 4f - 1f, -2f);
    }
}
