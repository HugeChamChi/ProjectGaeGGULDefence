using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스택 UI 시안 C — 10칸 게이지. 한 칸 = 최대치의 1/10(최대 100이면 10스택)이고 칸 안이 부분적으로 찬다.</summary>
public sealed class HackGaugeCells : HackStackGauge
{
    private const int Cells = 10;
    private const float CellWidth = 22f, CellGap = 4f, CellHeight = 14f;
    private readonly Image[] _fills = new Image[Cells];
    private readonly Image _back;
    private readonly TextMeshProUGUI _label, _count;

    /// <inheritdoc/>
    public override string Name => "C 10칸";

    /// <summary>보스 옆에 붙는 칸 게이지를 만든다.</summary>
    public HackGaugeCells(RectTransform parent, TMP_FontAsset font, Material fontMaterial)
        : base(parent, font, fontMaterial, new Vector2(276f, 76f), new Vector2(0f, .5f))
    {
        _back = Box("Back", Root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(276f, 76f), Back);
        _label = Text("Label", Root, new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(120f, 30f), 22f, TextAlignmentOptions.Left);
        _count = Text("Count", Root, new Vector2(1f, 1f), new Vector2(-10f, -2f), new Vector2(160f, 40f), 34f, TextAlignmentOptions.Right);
        for (int i = 0; i < Cells; i++)
        {
            var pos = new Vector2(10f + i * (CellWidth + CellGap), 10f);
            Box("Cell" + i, Root, Vector2.zero, pos, new Vector2(CellWidth, CellHeight), Track);
            _fills[i] = Box("CellFill" + i, Root, Vector2.zero, pos, new Vector2(CellWidth, CellHeight), Color.white);
        }
    }

    /// <inheritdoc/>
    protected override void Render(float now, int shown, float ratio, float bump, float ignite)
    {
        Color c = TierColor(ratio, now, ignite);
        float per = Max / (float)Cells;
        int last = Mathf.Clamp(Mathf.CeilToInt(shown / per) - 1, 0, Cells - 1);
        for (int i = 0; i < Cells; i++)
        {
            float part = Mathf.Clamp01((shown - i * per) / per);
            Fill(_fills[i], CellWidth, part);
            _fills[i].color = i == last ? Color.Lerp(c, Color.white, bump) : c;
        }
        string label = ratio >= 1f ? "HACK MAX" : "HACK";
        if (_label.text != label) _label.text = label;
        _label.color = c;
        string count = Fraction(shown);
        if (_count.text != count) _count.text = count;
        _count.color = Color.Lerp(Color.white, c, bump * .5f);
        _back.color = Color.Lerp(Back, new Color(c.r, c.g, c.b, .9f), ignite * .5f);
    }
}
