using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TimerBonusEase;

/// <summary>
/// 공지 C 젤리 필 — 가장 '쫀득한' 안.
/// 금테 알약이 가로·세로가 엇박으로 튀어나오며 젤리처럼 출렁이고, 제목 글자가 위에서 하나씩 통통 떨어진다.
/// 사라질 땐 세로로 꾹 눌려 터지며 테두리에서 방울이 튄다.
/// </summary>
public sealed class NoticeJellyPill : IBossClearNotice
{
    private const int KeyPop = 5301;
    private const float PillWidth = 780f;
    private const float PillHeight = 176f;
    private const float OutlinePad = 10f;
    private const float SliceBorder = 31f;          // TimerBonusSprites.Pill 9-slice 테두리(px)
    private const float PopSeconds = 0.11f;
    private const float PopLag = 0.03f;              // 세로가 가로보다 늦게 → 출렁임
    private const float JellyStart = 0.14f;
    private const float CharStart = 0.07f;
    private const float CharStagger = 0.035f;
    private const float CharDrop = 0.16f;
    private const float CharDropHeight = 70f;
    private const float SubStart = 0.32f;
    private const float SubPop = 0.1f;
    private const float ExitSeconds = 0.13f;
    private const float TitleY = 24f;
    private const float SubY = -48f;
    private const int PopDots = 18;

    private BossClearNoticeLab _lab;
    private Image _outline;
    private Image _pill;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _sub;
    private NoticeCharFx.CharTransform _charFx;
    private float _n;
    private Color[] _popColors;

    /// <inheritdoc />
    public string Name => "C 젤리 필";

    /// <inheritdoc />
    public void Setup(BossClearNoticeLab lab)
    {
        _lab = lab;
        var s = lab.Settings;
        _outline = NewPill("JellyPill_Outline", s.Gold, PillWidth + OutlinePad * 2f, PillHeight + OutlinePad * 2f);
        _pill = NewPill("JellyPill_Body", s.PillColor, PillWidth, PillHeight);
        _title = lab.CreateText("JellyPill_Title", 78f, Color.white);
        _sub = lab.CreateText("JellyPill_Sub", 38f, s.Gold);
        _charFx = CharDropIn;
        _popColors = new[] { s.Gold, Color.white };
        SetVisible(false);
    }

    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        _outline.gameObject.SetActive(visible);
        _pill.gameObject.SetActive(visible);
        _title.gameObject.SetActive(visible);
        _sub.gameObject.SetActive(visible);
    }

    /// <inheritdoc />
    public void Render(float n, float exitAt, float exitScale)
    {
        _n = n;
        float exitDur = ExitSeconds * exitScale;
        if (n < 0f || n >= exitAt + exitDur) { HideAll(); return; }
        float k = n >= exitAt ? Mathf.Clamp01((n - exitAt) / exitDur) : 0f;
        TimerBonusLab.SetText(_title, _lab.Title);
        TimerBonusLab.SetText(_sub, _lab.SubText);

        // 알약: 가로가 먼저, 세로가 한 박자 늦게 튀어나와 → 엇박 스프링으로 출렁 → 사라질 땐 세로로 꾹
        float sx, sy;
        if (n < JellyStart)
        {
            sx = OutBack(Mathf.Clamp01(n / PopSeconds), 2.8f);
            sy = OutBack(Mathf.Clamp01((n - PopLag) / PopSeconds), 2.8f);
        }
        else
        {
            float w = Spring(n - JellyStart, 20f, 6f);
            sx = 1f + 0.14f * w;
            sy = 1f - 0.14f * w;
        }
        sx *= 1f + 0.25f * k;
        sy *= 1f - InCubic(k);
        float alpha = 1f - InQuad(k);
        var scale = new Vector2(Mathf.Max(0f, sx), Mathf.Max(0f, sy));
        TimerBonusLab.Place(_outline, Vector2.zero, scale, alpha);
        TimerBonusLab.Place(_pill, Vector2.zero, scale, alpha);

        // 제목: 글자마다 위에서 통통 떨어진다
        TimerBonusLab.Place(_title, new Vector2(0f, TitleY * scale.y), new Vector2(1f, 1f - InCubic(k)), alpha);
        NoticeCharFx.Apply(_title, _charFx);

        if (n >= SubStart)
        {
            float q = Mathf.Clamp01((n - SubStart) / SubPop);
            float pop = OutBack(q, 3f);
            TimerBonusLab.Place(_sub, new Vector2(0f, SubY * scale.y), new Vector2(pop, pop * (1f - InCubic(k))), q * alpha);
        }
        else TimerBonusLab.Hide(_sub);

        if (k > 0f && _lab.Once(KeyPop))
        {
            for (int i = 0; i < PopDots; i++)
            {
                float ang = i * Mathf.PI * 2f / PopDots;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                _lab.Emit(new TimerBonusParticles.Particle
                {
                    Kind = TimerBonusParticles.Kind.Dot,
                    Position = new Vector2(dir.x * PillWidth * 0.5f, dir.y * PillHeight * 0.5f),
                    Velocity = dir * TimerBonusLab.Rnd(300f, 520f),
                    Drag = 0.08f,
                    Size = TimerBonusLab.Rnd(5f, 9f),
                    MaxLife = TimerBonusLab.Rnd(0.2f, 0.32f),
                    Color = _popColors[i % _popColors.Length],
                });
            }
        }
    }

    private void CharDropIn(int index, int count, out Vector2 offset, out float scale, out float alpha)
    {
        float q = Mathf.Clamp01((_n - (CharStart + index * CharStagger)) / CharDrop);
        float e = OutBack(q, 3f);
        offset = new Vector2(0f, CharDropHeight * (1f - e));
        scale = Mathf.LerpUnclamped(0.3f, 1f, e);
        alpha = Mathf.Clamp01(q * 4f);
    }

    private Image NewPill(string name, Color color, float width, float height)
    {
        var img = _lab.CreateImage(name, color, _lab.Sprites.Pill);
        img.type = Image.Type.Sliced;
        // 9-slice 테두리를 높이의 절반으로 키워 양끝을 반원으로
        img.pixelsPerUnitMultiplier = SliceBorder / (height * 0.5f);
        img.rectTransform.sizeDelta = new Vector2(width, height);
        return img;
    }

    private void HideAll()
    {
        TimerBonusLab.Hide(_outline);
        TimerBonusLab.Hide(_pill);
        TimerBonusLab.Hide(_title);
        TimerBonusLab.Hide(_sub);
    }
}
