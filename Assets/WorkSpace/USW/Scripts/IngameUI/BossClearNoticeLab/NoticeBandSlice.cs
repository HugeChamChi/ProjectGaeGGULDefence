using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TimerBonusEase;

/// <summary>
/// 공지 A 밴드 슬라이스 — 참고 GIF 1(WAVE 64)형.
/// 어두운 띠가 위아래로 탁 열리고(살짝 넘쳤다 돌아옴), 제목이 오른쪽에서 미끄러져 들어와 한 번 더 크게 튕긴다.
/// 사라질 땐 제목이 왼쪽으로 빠지면서 띠가 위아래로 닫힌다.
/// </summary>
public sealed class NoticeBandSlice : IBossClearNotice
{
    private const int KeyArrive = 5101;
    private const float BandWidth = 1400f;
    private const float BandHeight = 190f;
    private const float EdgeHeight = 5f;
    private const float OpenSeconds = 0.07f;
    private const float EdgeSeconds = 0.1f;
    private const float TitleStart = 0.04f;
    private const float TitleSlide = 0.12f;
    private const float SubStart = 0.2f;
    private const float SubFade = 0.1f;
    internal const float ExitSeconds = 0.14f;
    private const float TitleFromX = 460f;
    private const float TitleOutX = -560f;
    private const float TitleY = 22f;
    private const float SubY = -50f;
    private static readonly Color[] StreakColors = { Color.white, new Color(1f, 0.92f, 0.7f) };

    private BossClearNoticeLab _lab;
    private Image _band;
    private Image _edgeTop;
    private Image _edgeBottom;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _sub;

    /// <inheritdoc />
    public string Name => "A 밴드 슬라이스";

    /// <inheritdoc />
    public void Setup(BossClearNoticeLab lab)
    {
        _lab = lab;
        var s = lab.Settings;
        _band = lab.CreateImage("BandSlice_Band", s.DarkBand);
        _band.rectTransform.sizeDelta = new Vector2(BandWidth, BandHeight);
        _edgeTop = lab.CreateImage("BandSlice_EdgeTop", s.Gold);
        _edgeBottom = lab.CreateImage("BandSlice_EdgeBottom", s.Gold);
        _edgeTop.rectTransform.sizeDelta = _edgeBottom.rectTransform.sizeDelta = new Vector2(BandWidth, EdgeHeight);
        _title = lab.CreateText("BandSlice_Title", 92f, Color.white);
        _sub = lab.CreateText("BandSlice_Sub", 42f, s.Gold);
        SetVisible(false);
    }

    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        if (_band != null) _band.gameObject.SetActive(visible);
        if (_edgeTop != null) _edgeTop.gameObject.SetActive(visible);
        if (_edgeBottom != null) _edgeBottom.gameObject.SetActive(visible);
        if (_title != null) _title.gameObject.SetActive(visible);
        if (_sub != null) _sub.gameObject.SetActive(visible);
    }

    /// <inheritdoc />
    public void Render(float n, float exitAt, float exitScale)
    {
        float exitDur = ExitSeconds * exitScale;
        if (n < 0f || n >= exitAt + exitDur) { HideAll(); return; }
        float k = n >= exitAt ? Mathf.Clamp01((n - exitAt) / exitDur) : 0f;
        TimerBonusLab.SetText(_title, _lab.Title);
        TimerBonusLab.SetText(_sub, _lab.SubText);

        // 띠: 위아래로 탁 열리며 살짝 넘쳤다가 스프링으로 제자리 → 사라질 땐 닫힘
        float bandY = n < OpenSeconds ? 1.12f * OutCubic(n / OpenSeconds) : 1f + 0.12f * Spring(n - OpenSeconds, 28f, 10f);
        bandY *= 1f - InCubic(k);
        TimerBonusLab.Place(_band, Vector2.zero, new Vector2(1f, bandY), 1f);
        float edgeX = OutCubic(Mathf.Clamp01(n / EdgeSeconds));
        float edgeOffset = BandHeight * 0.5f * bandY;
        TimerBonusLab.Place(_edgeTop, new Vector2(0f, edgeOffset), new Vector2(edgeX, 1f), 1f - k);
        TimerBonusLab.Place(_edgeBottom, new Vector2(0f, -edgeOffset), new Vector2(edgeX, 1f), 1f - k);

        // 제목: 오른쪽에서 가로로 늘어난 채 쏜살같이 들어와 → 도착하는 순간 한 번 더 크게 튕김(쫀득)
        if (n >= TitleStart)
        {
            float q = Mathf.Clamp01((n - TitleStart) / TitleSlide), e = OutExpo(q);
            float x = TitleFromX * (1f - e), sx = 1f + 0.5f * (1f - e), sy = 1f - 0.2f * (1f - e), alpha = Mathf.Clamp01(q * 3f);
            float arrive = TitleStart + TitleSlide;
            if (n >= arrive)
            {
                float d = Spring(n - arrive, 24f, 9f);
                sx = sy = 1f + 0.18f * d;
                if (_lab.Once(KeyArrive))
                    _lab.Burst(new Vector2(-120f, TitleY), 10, 900f, 1600f, StreakColors, size0: 2f, size1: 4f, life0: 0.12f, life1: 0.2f, angle: Mathf.PI, spread: 0.05f);
            }
            if (k > 0f)
            {
                x = TitleOutX * InCubic(k);
                sx = 1f + 0.6f * k;
                sy = 1f - 0.5f * k;
                alpha = 1f - k;
            }
            TimerBonusLab.Place(_title, new Vector2(x, TitleY), new Vector2(sx, sy), alpha);
        }
        else TimerBonusLab.Hide(_title);

        if (n >= SubStart)
        {
            float q = Mathf.Clamp01((n - SubStart) / SubFade);
            float y = SubY - 26f * (1f - OutBack(q));
            TimerBonusLab.Place(_sub, new Vector2(0f, y), new Vector2(1f, 1f - InCubic(k)), q * (1f - Mathf.Clamp01(k * 2f)));
        }
        else TimerBonusLab.Hide(_sub);
    }

    private void HideAll()
    {
        TimerBonusLab.Hide(_band);
        TimerBonusLab.Hide(_edgeTop);
        TimerBonusLab.Hide(_edgeBottom);
        TimerBonusLab.Hide(_title);
        TimerBonusLab.Hide(_sub);
    }
}
