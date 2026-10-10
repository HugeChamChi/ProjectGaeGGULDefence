using UnityEngine;
using static CenterToastMath;

/// <summary>
/// H = F 진입 + 카드째 겹쳐 올라가기 (사용자 요청 2026-10-09, G와 비교용).
/// 새 패널은 F처럼 오른쪽에서 쏘아져 들어온다. 연타하면 이전 패널이 통째로 조금씩 위로 밀려
/// 새 패널 뒤에 카드 더미처럼 겹친다 — 윗변 강조선만 층층이 삐죽 보이고, 위로 갈수록 작아지고 흐려진다.
/// 사라질 땐 위로 떠오르며 흐려진다.
/// </summary>
public sealed class CenterToastSlideCardsMotion : ICenterToastMotion
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

    private const float CardStep = 16f;           // 한 장 뒤로 갈 때마다 위로
    private const float CardAlpha = 0.7f;         // 한 장 뒤로 갈 때마다 투명도 배율
    private const float CardScale = 0.95f;        // 한 장 뒤로 갈 때마다 크기 배율
    private const float TextFadeSlot = 0.6f;      // 이 정도 밀리면 뒤 카드 글자는 꺼짐 (윗변만 보이므로)
    private const float ExitRise = 40f;

    /// <inheritdoc />
    public bool MergeRepeats => false;
    /// <inheritdoc />
    public bool SingleSlot => false;
    /// <inheritdoc />
    public bool UseModernFont => true;
    /// <inheritdoc />
    public float ExitSeconds => 0.22f;

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
        float enter = OutExpo(e.Age / EnterSeconds);

        float y = s.AnchorY + older * CardStep + ExitRise * OutCubic(k);
        e.Root.anchoredPosition = new Vector2(SlideFrom * (1f - enter), y);
        float sc = Mathf.Pow(CardScale, older);
        e.Root.localScale = new Vector3(sc * (1f + Stretch * (1f - enter)), sc, 1f);
        e.Group.alpha = Mathf.Clamp01(e.Age / FadeInSeconds) * Mathf.Pow(CardAlpha, older) * (1f - InQuad(k));

        float panel = OutExpo((e.Age - PanelStart) / PanelOpenSeconds);
        float flash = e.Age < FlashSeconds ? 1f - e.Age / FlashSeconds : 0f;
        CenterToastLinePanel.Apply(e, s, 1f, panel, 1f, flash);

        float lead = -LineLead * (1f - OutExpo(e.Age / LineLeadSeconds));
        var a = e.LineA.rectTransform.anchoredPosition;
        var b = e.LineB.rectTransform.anchoredPosition;
        e.LineA.rectTransform.anchoredPosition = new Vector2(lead, a.y);
        e.LineB.rectTransform.anchoredPosition = new Vector2(lead, b.y);

        float hide = Mathf.Clamp01(older / TextFadeSlot);
        e.Text.maxVisibleCharacters = Mathf.CeilToInt(e.Message.Length * Mathf.Max(Mathf.Clamp01((e.Age - TypeStart) / TypeSeconds), hide));
        e.Text.color = CenterToastParts.WithAlpha(s.ModernText, s.ModernText.a * (1f - hide));
    }
}
