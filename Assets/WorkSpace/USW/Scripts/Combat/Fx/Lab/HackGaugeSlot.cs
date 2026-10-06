using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스택 UI 시안 D — 보스 HP바 아래 디버프 줄의 첫 칸에 넣는 방식(기존 BossDebuffBar 칸 모양).
/// 옆의 화상 칸은 다른 디버프와 나란히 놓였을 때를 보여주는 예시 대역이다.
/// </summary>
public sealed class HackGaugeSlot : HackStackGauge
{
    private const float SlotSize = 80f;
    // 인게임 UI_BossHpBar의 디버프 줄 위치 (HP바 아래 10)·칸 간격(BossDebuffBar 6)과 같게
    private const float DebuffOffsetY = 10f;
    private const float SlotGap = 6f;
    private static readonly Color SlotBack = new Color(.12f, .08f, .16f, .9f);
    private readonly Image _border, _icon, _fill;
    private readonly TextMeshProUGUI _count;

    /// <inheritdoc/>
    public override string Name => "D 디버프칸";

    /// <summary>해킹 디버프 칸과 화상 예시 칸을 만든다.</summary>
    public HackGaugeSlot(RectTransform parent, TMP_FontAsset font, Material fontMaterial)
        : base(parent, font, fontMaterial, new Vector2(SlotSize, SlotSize), new Vector2(0f, 1f))
    {
        var mid = new Vector2(.5f, .5f);
        _border = Box("Glow", Root, mid, Vector2.zero, Vector2.one * (SlotSize + 10f), Color.clear);
        Box("Back", Root, mid, Vector2.zero, Vector2.one * SlotSize, SlotBack);
        _icon = Box("Icon", Root, mid, new Vector2(0f, 6f), Vector2.one * 52f, Color.white, HackGaugeSprites.Hex);
        Box("IconLine", Root, mid, new Vector2(0f, 6f), Vector2.one * 52f, new Color(1f, 1f, 1f, .7f), HackGaugeSprites.HexLine);
        Box("Track", Root, Vector2.zero, new Vector2(4f, 3f), new Vector2(SlotSize - 8f, 6f), Track);
        _fill = Box("Fill", Root, Vector2.zero, new Vector2(4f, 3f), new Vector2(SlotSize - 8f, 6f), Color.white);
        var badge = Box("Badge", Root, new Vector2(1f, 0f), new Vector2(4f, 8f), new Vector2(50f, 30f), new Color(.08f, .06f, .12f, .95f));
        _count = Text("Count", badge.transform, mid, Vector2.zero, new Vector2(50f, 30f), 24f, TextAlignmentOptions.Center);

        // 옆 칸: 다른 디버프(화상) 예시
        var burn = Box("BurnSlotExample", Root, new Vector2(0f, 1f), new Vector2(SlotSize + SlotGap, 0f), Vector2.one * SlotSize, SlotBack);
        Box("BurnIcon", burn.transform, mid, new Vector2(0f, 4f), Vector2.one * 46f, new Color(1f, .5f, .2f), HackGaugeSprites.Disc);
        var burnCount = Text("BurnCount", burn.transform, new Vector2(1f, 0f), new Vector2(-4f, 2f), new Vector2(40f, 28f), 22f, TextAlignmentOptions.BottomRight);
        burnCount.text = "3";
    }

    /// <inheritdoc/>
    protected override float HudScale => 1f;

    /// <inheritdoc/>
    protected override Vector2 HudAnchor(Rect bar) => new Vector2(bar.xMin, bar.yMin - DebuffOffsetY);

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        Color c = TierColor(ratio, now, ignite);
        _icon.color = Color.Lerp(c, Color.white, bump * .5f);
        _fill.color = c;
        Fill(_fill, SlotSize - 8f, ratio);
        _border.color = new Color(c.r, c.g, c.b, .6f * bump + .8f * ignite + (ratio >= 1f ? .5f : 0f));
        string count = shown.ToString();
        if (_count.text != count) _count.text = count;
        _count.color = ratio >= 1f ? c : Color.white;
    }
}
