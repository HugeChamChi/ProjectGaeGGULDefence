using UnityEngine;
using static CenterToastMath;
using static CenterToastParts;

/// <summary>
/// C 글래스 슬라이드 (모던·속도감) — 왼쪽에 강조 막대가 달린 둥근 유리 카드가 오른쪽에서 가로로 늘어난 채 쏘아져 들어와
/// 스냅으로 멈추고, 글자가 빠르게 타이핑된다. 카드 뒤에는 강조색 빛이 은은하게.
/// 연타하면 이전 카드가 왼쪽으로 쏘아져 빠지고 새 카드가 오른쪽에서 쏘아져 들어온다 (컨베이어처럼, 횟수 표시 없음).
/// 사라질 땐 왼쪽으로 가속하며 빠져나간다.
/// </summary>
public sealed class CenterToastGlassSlideMotion : ICenterToastMotion
{
    private const float FontRatio = 0.86f;
    private const float Height = 96f;
    private const float PadLeft = 18f;
    private const float BarWidth = 6f;
    private const float BarHeightRatio = 0.56f;
    private const float BarGap = 20f;
    private const float PadRight = 40f;
    private const float Radius = 18f;
    private const float BarRadius = 3f;
    private const float SlideFrom = 280f;
    private const float EnterSeconds = 0.2f;
    private const float Stretch = 0.25f;
    private const float FadeInSeconds = 0.06f;
    private const float BarStart = 0.04f;
    private const float BarGrowSeconds = 0.12f;
    private const float TypeStart = 0.06f;
    private const float TypeSeconds = 0.18f;
    private const float FlashSeconds = 0.18f;        // 들어오는 순간 막대·빛이 번쩍
    private const float GlowAlpha = 0.3f;
    private const float GlowFlash = 0.25f;
    private const float ExitSlide = -220f;
    private const float KickedExitSlide = -420f;     // 새 카드에 밀려날 땐 더 멀리, 더 빨리

    /// <inheritdoc />
    public bool MergeRepeats => false;
    /// <inheritdoc />
    public bool SingleSlot => true;
    /// <inheritdoc />
    public bool UseModernFont => true;
    /// <inheritdoc />
    public float ExitSeconds => 0.16f;

    /// <inheritdoc />
    public void Prepare(CenterToastEntry e, CenterToastSettings s, CenterToastSprites sprites)
    {
        Measure(e, s.FontSize * FontRatio);
        float width = PadLeft + BarWidth + BarGap + e.TextWidth + PadRight;
        e.Width = width;
        var accent = s.ModernAccent(e.Kind);
        Simple(e.Glow, sprites.Glow, WithAlpha(accent, GlowAlpha), new Vector2(width * 1.25f, Height * 2.2f));
        Rounded(e.Background, sprites, s.ModernGlass, new Vector2(width, Height), Radius);
        Rounded(e.LineA, sprites, accent, new Vector2(BarWidth, Height * BarHeightRatio), BarRadius);
        e.LineA.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + PadLeft + BarWidth * 0.5f, 0f);
        e.Text.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + PadLeft + BarWidth + BarGap + e.TextWidth * 0.5f, 0f);
        e.Text.color = s.ModernText;
        e.Text.maxVisibleCharacters = 0;
    }

    /// <inheritdoc />
    public void Render(CenterToastEntry e, int index, CenterToastSettings s)
    {
        float k = e.ExitProgress(ExitSeconds);
        float enter = OutExpo(e.Age / EnterSeconds);
        float flash = e.Age < FlashSeconds ? 1f - e.Age / FlashSeconds : 0f;
        var accent = s.ModernAccent(e.Kind);

        float x = SlideFrom * (1f - enter) + (e.Kicked ? KickedExitSlide : ExitSlide) * InCubic(k);
        e.Root.anchoredPosition = new Vector2(x, s.AnchorY);
        e.Root.localScale = new Vector3(1f + Stretch * (1f - enter) + 0.08f * InCubic(k), 1f, 1f);
        e.Group.alpha = Mathf.Clamp01(e.Age / FadeInSeconds) * (1f - k);

        e.LineA.rectTransform.localScale = new Vector3(1f, OutBack((e.Age - BarStart) / BarGrowSeconds, 2.5f), 1f);
        e.LineA.color = Color.Lerp(accent, Color.white, flash);
        e.Glow.color = WithAlpha(accent, GlowAlpha * enter + GlowFlash * flash);

        // 빠른 타이핑
        int len = e.Message.Length;
        e.Text.maxVisibleCharacters = Mathf.CeilToInt(len * Mathf.Clamp01((e.Age - TypeStart) / TypeSeconds));
    }
}
