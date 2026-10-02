using UnityEngine;
using static CenterToastMath;
using static CenterToastParts;

/// <summary>
/// A 스택 (모던) — 참고 GIF(중앙팝업1)의 '연타하면 쌓인다'는 동작은 그대로, 모양은 모던하게.
/// 줄마다 작은 어두운 유리 캡슐 + 왼쪽 강조 점 + 고딕 글자. 새 줄은 아래에서 스냅(OutExpo)으로 치고 올라오며
/// 가로로 살짝 늘었다 돌아오고, 빛 한 줄이 캡슐을 스윽 훑는다. 이전 줄들은 위로 밀리며 작아지고 빠르게 흐려진다.
/// </summary>
public sealed class CenterToastStackMotion : ICenterToastMotion
{
    private const float FontRatio = 0.86f;
    private const float Height = 76f;
    private const float PadX = 30f;
    private const float Radius = 16f;
    private const float Dot = 12f;
    private const float DotGap = 14f;
    private const float DotPopSeconds = 0.16f;
    private const float EnterSeconds = 0.18f;
    private const float EnterDrop = 40f;
    private const float FadeInSeconds = 0.07f;
    private const float StretchX = 0.06f;
    private const float OlderScale = 0.94f;      // 한 줄 위로 갈 때마다 크기 배율
    private const float OlderAlpha = 0.6f;       // 한 줄 위로 갈 때마다 투명도 배율
    private const float SweepStart = 0.04f;
    private const float SweepSeconds = 0.34f;
    private const float SweepWidth = 46f;
    private const float SweepTilt = 18f;
    private const float SweepAlpha = 0.22f;
    private const float FlashSeconds = 0.2f;
    private const float ExitRise = 16f;

    /// <inheritdoc />
    public bool MergeRepeats => false;
    /// <inheritdoc />
    public bool SingleSlot => false;
    /// <inheritdoc />
    public bool UseModernFont => true;
    /// <inheritdoc />
    public float ExitSeconds => 0.2f;

    /// <inheritdoc />
    public void Prepare(CenterToastEntry e, CenterToastSettings s, CenterToastSprites sprites)
    {
        Measure(e, s.FontSize * FontRatio);
        float width = PadX * 2f + Dot + DotGap + e.TextWidth;
        e.Width = width;
        Rounded(e.Background, sprites, s.ModernGlass, new Vector2(width, Height), Radius);
        Simple(e.Icon, sprites.Circle, s.ModernAccent(e.Kind), Vector2.one * Dot);
        e.Icon.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + PadX + Dot * 0.5f, 0f);
        e.Clip.sizeDelta = new Vector2(width, Height);   // 빛 스윕이 캡슐 밖으로 안 나가게
        e.Text.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + PadX + Dot + DotGap + e.TextWidth * 0.5f, 0f);
        Plain(e.Sweep, WithAlpha(Color.white, SweepAlpha), new Vector2(SweepWidth, Height * 2f));
        e.Sweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -SweepTilt);
    }

    /// <inheritdoc />
    public void Render(CenterToastEntry e, int index, CenterToastSettings s)
    {
        float k = e.ExitProgress(ExitSeconds);
        float enter = OutExpo(e.Age / EnterSeconds);
        float older = Mathf.Max(0f, e.Slot);
        float step = Height + s.StackGap;
        float sc = Mathf.Pow(OlderScale, older);

        e.Root.anchoredPosition = new Vector2(0f, s.AnchorY + e.Slot * step - EnterDrop * (1f - enter) + ExitRise * OutCubic(k));
        e.Root.localScale = new Vector3(sc * (1f + StretchX * (1f - enter)), sc * Mathf.Lerp(0.6f, 1f, enter) * (1f - 0.25f * k), 1f);
        e.Group.alpha = Mathf.Clamp01(e.Age / FadeInSeconds) * Mathf.Pow(OlderAlpha, older) * (1f - k);

        e.Icon.rectTransform.localScale = Vector3.one * OutBack(e.Age / DotPopSeconds, 3f);
        e.Text.color = e.Kind == CenterToastKind.Warning
            ? Color.Lerp(s.ModernAccent(e.Kind), s.ModernText, e.Age / FlashSeconds)
            : s.ModernText;

        float g = (e.Age - SweepStart) / SweepSeconds;
        bool sweep = g > 0f && g < 1f;
        e.Sweep.enabled = sweep;
        if (sweep)
        {
            float half = e.Width * 0.5f + SweepWidth;
            e.Sweep.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, OutCubic(g)), 0f);
        }
    }
}
