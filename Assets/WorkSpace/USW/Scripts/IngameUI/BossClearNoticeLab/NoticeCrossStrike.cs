using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TimerBonusEase;

/// <summary>
/// 공지 D 크로스 스트라이크 — 가장 텐션 높은 안.
/// 금색·분홍 띠 두 줄이 양쪽에서 엇갈려 쏘아져 가운데서 맞부딪히는 순간, 제목이 크게 박히며
/// 타이머 실험실과 같은 히트스톱·플래시·줌이 터지고 RGB 잔상이 갈라진다.
/// 사라질 땐 띠가 가던 방향으로 계속 빠져나가고 제목은 가로로 찢어지듯 늘어나며 사라진다.
/// </summary>
public sealed class NoticeCrossStrike : IBossClearNotice
{
    private const int KeyHit = 5401;
    private const float StripeWidth = 1500f;
    private const float StripeHeight = 54f;
    private const float StripeGap = 64f;
    private const float StripeTilt = 4f;
    private const float StripeFromX = 1500f;
    private const float StripeOutX = 1600f;
    private const float SweepSeconds = 0.09f;
    private const float SmashSeconds = 0.06f;
    private const float FlashSeconds = 0.12f;
    private const float FlashHeight = 230f;
    private const float RgbSeconds = 0.3f;
    private const float RgbOffset = 16f;
    private const float SubStart = 0.24f;
    private const float SubSlide = 0.1f;
    private const float ExitSeconds = 0.14f;
    private const float TitleY = 18f;
    private const float SubY = -52f;
    private const float TitleOutline = 0.2f;

    private BossClearNoticeLab _lab;
    private Image _stripeTop;
    private Image _stripeBottom;
    private Image _flash;
    private TextMeshProUGUI _ghostCyan;
    private TextMeshProUGUI _ghostHot;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _sub;
    private Color[] _sparkColors;

    /// <inheritdoc />
    public string Name => "D 크로스 스트라이크";

    /// <inheritdoc />
    public void Setup(BossClearNoticeLab lab)
    {
        _lab = lab;
        var s = lab.Settings;
        _flash = lab.CreateImage("CrossStrike_Flash", Color.white, lab.Sprites.Glow);
        _flash.rectTransform.sizeDelta = new Vector2(StripeWidth, FlashHeight);
        _stripeTop = lab.CreateImage("CrossStrike_StripeTop", s.Gold);
        _stripeBottom = lab.CreateImage("CrossStrike_StripeBottom", s.Hot);
        _stripeTop.rectTransform.sizeDelta = _stripeBottom.rectTransform.sizeDelta = new Vector2(StripeWidth, StripeHeight);
        _ghostCyan = lab.CreateText("CrossStrike_GhostCyan", 96f, s.Cyan);
        _ghostHot = lab.CreateText("CrossStrike_GhostHot", 96f, s.Hot);
        _title = lab.CreateText("CrossStrike_Title", 96f, Color.white);
        _title.outlineWidth = TitleOutline;
        _title.outlineColor = Color.black;
        _sub = lab.CreateText("CrossStrike_Sub", 40f, s.Gold);
        _sparkColors = new[] { Color.white, s.Gold, s.Hot };
        SetVisible(false);
    }

    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        _flash.gameObject.SetActive(visible);
        _stripeTop.gameObject.SetActive(visible);
        _stripeBottom.gameObject.SetActive(visible);
        _ghostCyan.gameObject.SetActive(visible);
        _ghostHot.gameObject.SetActive(visible);
        _title.gameObject.SetActive(visible);
        _sub.gameObject.SetActive(visible);
    }

    /// <inheritdoc />
    public void Render(float n, float exitAt, float exitScale)
    {
        float exitDur = ExitSeconds * exitScale;
        if (n < 0f || n >= exitAt + exitDur) { HideAll(); return; }
        float k = n >= exitAt ? Mathf.Clamp01((n - exitAt) / exitDur) : 0f;
        string title = _lab.Title;
        TimerBonusLab.SetText(_title, title);
        TimerBonusLab.SetText(_ghostCyan, title);
        TimerBonusLab.SetText(_ghostHot, title);
        TimerBonusLab.SetText(_sub, _lab.SubText);

        // 띠 두 줄: 양쪽에서 엇갈려 들어와 가운데서 멈춤 → 사라질 땐 가던 방향으로 계속
        float e = OutCubic(Mathf.Clamp01(n / SweepSeconds));
        float wobble = n >= SweepSeconds ? 1f + 0.4f * Spring(n - SweepSeconds, 30f, 10f) : 1f;
        float leave = InCubic(k);
        float topX = -StripeFromX * (1f - e) + StripeOutX * leave;
        float bottomX = StripeFromX * (1f - e) - StripeOutX * leave;
        TimerBonusLab.Place(_stripeTop, new Vector2(topX, StripeGap), new Vector2(1f, wobble), 1f, StripeTilt);
        TimerBonusLab.Place(_stripeBottom, new Vector2(bottomX, -StripeGap), new Vector2(1f, wobble), 1f, StripeTilt);

        // 맞부딪히는 순간: 히트스톱·플래시·줌 + 좌우로 불꽃
        bool hit = n >= SweepSeconds;
        if (hit && _lab.Once(KeyHit))
        {
            float impact = _lab.Settings.StrikeImpact;
            if (impact > 0f) _lab.TimerLab.Impact(impact);
            _lab.Burst(Vector2.zero, 16, 700f, 1500f, _sparkColors, size0: 3f, size1: 6f, life0: 0.14f, life1: 0.26f, angle: 0f, spread: 0.12f);
            _lab.Burst(Vector2.zero, 16, 700f, 1500f, _sparkColors, size0: 3f, size1: 6f, life0: 0.14f, life1: 0.26f, angle: Mathf.PI, spread: 0.12f);
        }
        float flash = hit ? 1f - Mathf.Clamp01((n - SweepSeconds) / FlashSeconds) : 0f;
        TimerBonusLab.Place(_flash, Vector2.zero, new Vector2(1f, 1f + 0.5f * flash), flash);

        // 제목: 크게 → 박힘 → 살짝 출렁, RGB 잔상이 좁혀짐 → 사라질 땐 가로로 찢어지듯
        if (hit)
        {
            float q = Mathf.Clamp01((n - SweepSeconds) / SmashSeconds);
            float sc = Mathf.Lerp(2.2f, 1f, InQuad(q));
            if (q >= 1f) sc *= 1f + 0.08f * Spring(n - SweepSeconds - SmashSeconds, 26f, 8f);
            float sx = sc * (1f + 0.6f * k), sy = sc * (1f - InCubic(k));
            float alpha = Mathf.Clamp01(q * 3f) * (1f - k);
            float rgb = RgbOffset * (1f - Mathf.Clamp01((n - SweepSeconds) / RgbSeconds)) + 20f * k;
            float ghostAlpha = rgb > 0.5f ? 0.85f * alpha : 0f;
            var size = new Vector2(sx, sy);
            TimerBonusLab.Place(_title, new Vector2(0f, TitleY), size, alpha);
            TimerBonusLab.Place(_ghostCyan, new Vector2(-rgb, TitleY), size, ghostAlpha);
            TimerBonusLab.Place(_ghostHot, new Vector2(rgb, TitleY), size, ghostAlpha);
        }
        else
        {
            TimerBonusLab.Hide(_title);
            TimerBonusLab.Hide(_ghostCyan);
            TimerBonusLab.Hide(_ghostHot);
        }

        if (n >= SubStart)
        {
            float q = Mathf.Clamp01((n - SubStart) / SubSlide);
            TimerBonusLab.Place(_sub, new Vector2(0f, SubY - 20f * (1f - OutCubic(q))), new Vector2(1f, 1f - InCubic(k)), q * (1f - k));
        }
        else TimerBonusLab.Hide(_sub);
    }

    private void HideAll()
    {
        TimerBonusLab.Hide(_flash);
        TimerBonusLab.Hide(_stripeTop);
        TimerBonusLab.Hide(_stripeBottom);
        TimerBonusLab.Hide(_ghostCyan);
        TimerBonusLab.Hide(_ghostHot);
        TimerBonusLab.Hide(_title);
        TimerBonusLab.Hide(_sub);
    }
}
