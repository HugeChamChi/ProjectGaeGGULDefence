using UnityEngine;
using static CenterToastMath;

/// <summary>
/// I = G + 화면 전체 폭 어두운 띠 (사용자 요청 2026-10-09, 참고 GIF(중앙팝업1)의 띠 비율).
/// 바탕이 글자 폭 패널 대신 화면 가로를 다 덮고 위아래가 부드럽게 사라지는 띠 — 제자리에서 위아래로 열린다.
/// 글자와 강조선은 G처럼 오른쪽에서 쏘아져 들어오고 타이핑된다. 연타하면 이전 알림의 띠는 꺼지고
/// 글자만 띠 위로 올라가 촘촘히 겹쳐 쌓이며 흐려진다. 혼자 남아도 사라질 땐 띠가 먼저 꺼지고 글자가 떠오른다.
/// 진하게 보이는 띠 높이가 Scale 1.2에서 화면 폭의 약 11.6% (참고 GIF 실측)가 되도록 잡았다.
/// </summary>
public sealed class CenterToastSlideBandMotion : ICenterToastMotion
{
    private const float FontRatio = 0.88f;
    private const float BandWidth = 2800f;        // 화면(1080)보다 넓게 — SoftBar 양끝 흐림이 화면 밖에 오도록
    private const float BandHeight = 140f;        // 위아래 흐림을 빼고 진하게 보이는 높이 ≈ 104
    private const float LineHeight = 72f;         // 강조선은 띠의 진한 부분 안쪽에서 글자를 감싼다
    private const float PadX = 60f;
    private const float SlideFrom = 300f;
    private const float EnterSeconds = 0.2f;
    private const float Stretch = 0.25f;
    private const float LineLead = 70f;
    private const float LineLeadSeconds = 0.12f;
    private const float BandOpenSeconds = 0.12f;
    private const float TypeStart = 0.06f;
    private const float TypeSeconds = 0.18f;
    private const float FlashSeconds = 0.14f;
    private const float FadeInSeconds = 0.05f;

    private const float FirstRise = 76f;          // 첫 번째 이전 글자 — 띠 윗변 바로 위로
    private const float RiseStep = 18f;
    private const float GhostAlpha = 0.62f;
    private const float GhostScale = 0.97f;
    private const float GhostTint = 0.78f;
    private const float BandOffSlot = 0.45f;      // 이 정도 밀리면 띠·선이 다 꺼짐
    private const float ExitRise = 46f;

    /// <inheritdoc />
    public bool MergeRepeats => false;
    /// <inheritdoc />
    public bool SingleSlot => false;
    /// <inheritdoc />
    public bool UseModernFont => true;
    /// <inheritdoc />
    public float ExitSeconds => 0.24f;

    /// <inheritdoc />
    public void Prepare(CenterToastEntry e, CenterToastSettings s, CenterToastSprites sprites)
    {
        CenterToastLinePanel.Prepare(e, s, FontRatio, LineHeight, PadX);
        e.Background.enabled = false;
        CenterToastParts.Simple(e.Glow, sprites.SoftBar, s.ModernGlass, new Vector2(BandWidth, BandHeight));
        e.Text.maxVisibleCharacters = 0;
    }

    /// <inheritdoc />
    public void Render(CenterToastEntry e, int index, CenterToastSettings s)
    {
        float k = e.ExitProgress(ExitSeconds);
        float older = Mathf.Max(0f, e.Slot);
        float ghost = Mathf.Clamp01(older / BandOffSlot);
        float enter = OutExpo(e.Age / EnterSeconds);

        // 루트는 제자리(띠 기준). 글자·선만 오른쪽에서 들어온다.
        float rise = older < 1f ? FirstRise * OutCubic(older) : FirstRise + (older - 1f) * RiseStep;
        e.Root.anchoredPosition = new Vector2(0f, s.AnchorY + rise + ExitRise * OutCubic(k));
        float sc = Mathf.Pow(GhostScale, older);
        e.Root.localScale = new Vector3(sc, sc, 1f);
        e.Group.alpha = Mathf.Clamp01(e.Age / FadeInSeconds) * Mathf.Pow(GhostAlpha, older) * (1f - InQuad(k));

        float slide = SlideFrom * (1f - enter);
        float stretch = 1f + Stretch * (1f - enter);
        e.Clip.anchoredPosition = new Vector2(slide, 0f);
        e.Clip.localScale = new Vector3(stretch, 1f, 1f);

        // 띠: 제자리에서 위아래로 열리고, 밀려 올라가면 꺼진다 (글자만 남음)
        float band = OutExpo(e.Age / BandOpenSeconds);
        float bandAlpha = 1f - ghost;
        if (index == 0) bandAlpha *= 1f - OutCubic(k / 0.6f);
        e.Glow.rectTransform.localScale = new Vector3(1f, band, 1f);
        e.Glow.rectTransform.anchoredPosition = new Vector2(0f, -rise);   // 띠는 올라가지 않고 그 자리에서 꺼진다
        e.Glow.color = CenterToastParts.WithAlpha(s.ModernGlass, s.ModernGlass.a * bandAlpha);

        float flash = e.Age < FlashSeconds ? 1f - e.Age / FlashSeconds : 0f;
        CenterToastLinePanel.Apply(e, s, 1f, band, 1f, flash);
        e.LineA.color = e.LineB.color = CenterToastParts.WithAlpha(e.LineA.color, bandAlpha);
        float lead = -LineLead * (1f - OutExpo(e.Age / LineLeadSeconds));
        var a = e.LineA.rectTransform.anchoredPosition;
        var b = e.LineB.rectTransform.anchoredPosition;
        e.LineA.rectTransform.anchoredPosition = new Vector2(slide + lead, a.y);
        e.LineB.rectTransform.anchoredPosition = new Vector2(slide + lead, b.y);
        e.LineA.rectTransform.localScale = e.LineB.rectTransform.localScale = new Vector3(stretch, 1f, 1f);

        float typed = Mathf.Max(Mathf.Clamp01((e.Age - TypeStart) / TypeSeconds), ghost);
        e.Text.maxVisibleCharacters = Mathf.CeilToInt(e.Message.Length * typed);
        var dim = new Color(s.ModernText.r * GhostTint, s.ModernText.g * GhostTint, s.ModernText.b * GhostTint, s.ModernText.a);
        e.Text.color = Color.Lerp(s.ModernText, dim, ghost);
    }
}
