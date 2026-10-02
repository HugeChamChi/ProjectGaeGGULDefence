using UnityEngine;
using static CenterToastMath;

/// <summary>
/// F = B 디자인 + C 연출 — B의 라인 패널(각진 유리 + 위·아래 강조선)이 오른쪽에서 가로로 늘어난 채 쏘아져 들어와 스냅으로 멈춘다.
/// 강조선이 패널보다 한 박자 먼저 도착해 끌고 들어오는 느낌, 패널은 들어오면서 위아래로 열리고 글자는 빠르게 타이핑된다.
/// 연타하면 이전 패널이 왼쪽으로 쏘아져 빠지고 새 패널이 오른쪽에서 들어온다 (컨베이어). 사라질 땐 왼쪽으로 가속하며 빠진다.
/// </summary>
public sealed class CenterToastSlideLinesMotion : ICenterToastMotion
{
    private const float FontRatio = 0.88f;
    private const float Height = 96f;
    private const float PadX = 60f;
    private const float SlideFrom = 300f;
    private const float EnterSeconds = 0.2f;
    private const float Stretch = 0.25f;
    private const float LineLead = 70f;              // 강조선이 패널보다 앞서 도착하는 거리
    private const float LineLeadSeconds = 0.12f;
    private const float PanelStart = 0.03f;
    private const float PanelOpenSeconds = 0.12f;
    private const float TypeStart = 0.06f;
    private const float TypeSeconds = 0.18f;
    private const float FlashSeconds = 0.14f;
    private const float FadeInSeconds = 0.05f;
    private const float ExitSlide = -220f;
    private const float KickedExitSlide = -420f;     // 새 패널에 밀려날 땐 더 멀리, 더 빨리

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
        CenterToastLinePanel.Prepare(e, s, FontRatio, Height, PadX);
        e.Text.maxVisibleCharacters = 0;
    }

    /// <inheritdoc />
    public void Render(CenterToastEntry e, int index, CenterToastSettings s)
    {
        float k = e.ExitProgress(ExitSeconds);
        float enter = OutExpo(e.Age / EnterSeconds);
        float x = SlideFrom * (1f - enter) + (e.Kicked ? KickedExitSlide : ExitSlide) * InCubic(k);
        e.Root.anchoredPosition = new Vector2(x, s.AnchorY);
        e.Root.localScale = new Vector3(1f + Stretch * (1f - enter) + 0.08f * InCubic(k), 1f, 1f);
        e.Group.alpha = Mathf.Clamp01(e.Age / FadeInSeconds) * (1f - k);

        float panel = OutExpo((e.Age - PanelStart) / PanelOpenSeconds);
        float flash = e.Age < FlashSeconds ? 1f - e.Age / FlashSeconds : 0f;
        CenterToastLinePanel.Apply(e, s, 1f, panel, 1f, flash);

        // 선이 패널보다 앞서(왼쪽으로) 도착 — 들어오는 방향의 앞쪽으로 살짝 밀어 둔다
        float lead = -LineLead * (1f - OutExpo(e.Age / LineLeadSeconds));
        var a = e.LineA.rectTransform.anchoredPosition;
        var b = e.LineB.rectTransform.anchoredPosition;
        e.LineA.rectTransform.anchoredPosition = new Vector2(lead, a.y);
        e.LineB.rectTransform.anchoredPosition = new Vector2(lead, b.y);

        // 빠른 타이핑
        e.Text.maxVisibleCharacters = Mathf.CeilToInt(e.Message.Length * Mathf.Clamp01((e.Age - TypeStart) / TypeSeconds));
    }
}
