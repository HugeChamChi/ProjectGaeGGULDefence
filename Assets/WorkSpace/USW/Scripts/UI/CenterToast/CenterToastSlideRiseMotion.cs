using UnityEngine;
using static CenterToastMath;

/// <summary>
/// G = F 진입 + 참고 GIF(중앙팝업1)식 겹쳐 올라가기 (사용자 요청 2026-10-09).
/// 새 패널은 F처럼 오른쪽에서 쏘아져 들어오고 글자가 타이핑된다.
/// 연타하면 이전 패널은 왼쪽으로 빠지지 않고 바탕·강조선이 꺼지며 글자만 패널 위로 올라가,
/// 이전 글자들끼리 촘촘히 겹쳐 쌓이고 위로 갈수록 흐려진다. 혼자 남은 패널도 사라질 땐 위로 떠오르며 흐려진다.
/// </summary>
public sealed class CenterToastSlideRiseMotion : ICenterToastMotion
{
    private const float FontRatio = 0.88f;
    private const float Height = 96f;
    private const float PadX = 60f;
    private const float SlideFrom = 300f;
    private const float EnterSeconds = 0.2f;
    private const float Stretch = 0.25f;
    private const float LineLead = 70f;
    private const float LineLeadSeconds = 0.12f;
    private const float PanelStart = 0.03f;
    private const float PanelOpenSeconds = 0.12f;
    private const float TypeStart = 0.06f;
    private const float TypeSeconds = 0.18f;
    private const float FlashSeconds = 0.14f;
    private const float FadeInSeconds = 0.05f;

    private const float FirstRise = 50f;          // 첫 번째 이전 글자 — 새 패널 윗변 바로 위로
    private const float RiseStep = 18f;           // 그 위로 쌓일 때마다 (글자 높이의 절반쯤 → 촘촘히 겹침)
    private const float GhostAlpha = 0.62f;       // 한 칸 위로 갈 때마다 투명도 배율
    private const float GhostScale = 0.97f;       // 한 칸 위로 갈 때마다 크기 배율
    private const float GhostTint = 0.78f;        // 이전 글자 밝기
    private const float PanelOffSlot = 0.45f;     // 이 정도 밀리면 바탕·선이 다 꺼짐
    private const float ExitRise = 46f;           // 사라질 때 더 떠오르는 거리

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
        CenterToastLinePanel.Prepare(e, s, FontRatio, Height, PadX);
        e.Text.maxVisibleCharacters = 0;
    }

    /// <inheritdoc />
    public void Render(CenterToastEntry e, int index, CenterToastSettings s)
    {
        float k = e.ExitProgress(ExitSeconds);
        float older = Mathf.Max(0f, e.Slot);
        float ghost = Mathf.Clamp01(older / PanelOffSlot);
        float enter = OutExpo(e.Age / EnterSeconds);

        float rise = older < 1f ? FirstRise * OutCubic(older) : FirstRise + (older - 1f) * RiseStep;
        float y = s.AnchorY + rise + ExitRise * OutCubic(k);
        e.Root.anchoredPosition = new Vector2(SlideFrom * (1f - enter), y);
        float sc = Mathf.Pow(GhostScale, older);
        e.Root.localScale = new Vector3(sc * (1f + Stretch * (1f - enter)), sc, 1f);
        e.Group.alpha = Mathf.Clamp01(e.Age / FadeInSeconds) * Mathf.Pow(GhostAlpha, older) * (1f - InQuad(k));

        // 패널: 진입 때 열리고, 밀려 올라가면 바탕·선이 꺼진다 (글자만 남음)
        float panel = OutExpo((e.Age - PanelStart) / PanelOpenSeconds);
        float flash = e.Age < FlashSeconds ? 1f - e.Age / FlashSeconds : 0f;
        CenterToastLinePanel.Apply(e, s, 1f, panel, 1f, flash);
        float panelAlpha = 1f - ghost;
        if (index == 0) panelAlpha *= 1f - OutCubic(k / 0.6f);   // 혼자 사라질 때도 바탕부터 꺼짐
        e.Background.color = CenterToastParts.WithAlpha(s.ModernGlass, s.ModernGlass.a * panelAlpha);
        e.LineA.color = e.LineB.color = CenterToastParts.WithAlpha(e.LineA.color, panelAlpha);

        float lead = -LineLead * (1f - OutExpo(e.Age / LineLeadSeconds));
        var a = e.LineA.rectTransform.anchoredPosition;
        var b = e.LineB.rectTransform.anchoredPosition;
        e.LineA.rectTransform.anchoredPosition = new Vector2(lead, a.y);
        e.LineB.rectTransform.anchoredPosition = new Vector2(lead, b.y);

        // 타이핑 중에 밀려나도 이전 글자는 문장 전체로 보인다
        float typed = Mathf.Max(Mathf.Clamp01((e.Age - TypeStart) / TypeSeconds), ghost);
        e.Text.maxVisibleCharacters = Mathf.CeilToInt(e.Message.Length * typed);
        var dim = new Color(s.ModernText.r * GhostTint, s.ModernText.g * GhostTint, s.ModernText.b * GhostTint, s.ModernText.a);
        e.Text.color = Color.Lerp(s.ModernText, dim, ghost);
    }
}
