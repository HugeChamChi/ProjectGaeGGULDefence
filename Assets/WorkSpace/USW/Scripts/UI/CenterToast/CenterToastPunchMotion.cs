using UnityEngine;
using UnityEngine.UI;
using static CenterToastMath;

/// <summary>
/// D 펀치 글자 — 바탕 없이 은은한 어두운 빛 위에 글자만. 크고 자간이 벌어진 채 → 꽉 박힌다.
/// 다시 누르면 이전 글자는 커지며 터지듯 사라지고 새 글자가 다시 박힌다 (연타할수록 탁탁탁 리듬).
/// 경고는 박히는 순간 강조색이었다가 흰색으로 식는다. 모던 A·B·C와 달리 게임 글꼴 + 외곽선.
/// </summary>
public sealed class CenterToastPunchMotion : ICenterToastMotion
{
    private const float PunchSeconds = 0.1f;
    private const float PunchFrom = 1.6f;
    private const float SpacingFrom = 30f;
    private const float SettleAmount = 0.06f;
    private const float ColorHold = 0.06f;
    private const float ColorFade = 0.2f;
    private const float GlowWidthRatio = 1.4f;
    private const float GlowHeightRatio = 1.6f;
    private const float GlowAlpha = 0.55f;
    private const float KickGrow = 0.3f;
    private const float ExitRise = 16f;

    /// <inheritdoc />
    public bool MergeRepeats => false;
    /// <inheritdoc />
    public bool SingleSlot => true;
    /// <inheritdoc />
    public bool UseModernFont => false;
    /// <inheritdoc />
    public float ExitSeconds => 0.14f;

    /// <inheritdoc />
    public void Prepare(CenterToastEntry e, CenterToastSettings s, CenterToastSprites sprites)
    {
        CenterToastParts.Measure(e, s.FontSize);
        e.Text.outlineWidth = s.OutlineWidth;
        e.Text.outlineColor = s.OutlineColor;
        e.Background.enabled = true;
        e.Background.sprite = sprites.Glow;
        e.Background.type = Image.Type.Simple;
        var bg = s.Background;
        e.Background.color = new Color(bg.r, bg.g, bg.b, GlowAlpha);
        e.Background.rectTransform.sizeDelta = new Vector2((e.TextWidth + s.PaddingX * 2f) * GlowWidthRatio, s.Height * GlowHeightRatio);
        e.Background.rectTransform.anchoredPosition = Vector2.zero;
        e.Text.rectTransform.anchoredPosition = Vector2.zero;
        e.Text.rectTransform.localScale = Vector3.one;
    }

    /// <inheritdoc />
    public void Render(CenterToastEntry e, int index, CenterToastSettings s)
    {
        float k = e.ExitProgress(ExitSeconds);
        float q = Mathf.Clamp01(e.Age / PunchSeconds);
        float sc = Mathf.Lerp(PunchFrom, 1f, OutCubic(q));
        if (q >= 1f) sc *= 1f + SettleAmount * Spring(e.Age - PunchSeconds, 24f, 8f);
        float y = 0f;
        if (k > 0f)
        {
            if (e.Kicked) sc *= 1f + KickGrow * k;       // 새 글자에 밀려나면 커지며 터지듯
            else y = ExitRise * k;                      // 그냥 끝나면 살짝 떠오르며
        }
        e.Text.characterSpacing = SpacingFrom * (1f - OutCubic(q));
        e.Root.anchoredPosition = new Vector2(0f, s.AnchorY + y);
        e.Root.localScale = Vector3.one * sc;
        e.Group.alpha = Mathf.Clamp01(q * 3f) * (1f - k);
        e.Text.color = e.Kind == CenterToastKind.Warning
            ? Color.Lerp(s.Accent(e.Kind), s.TextColor, (e.Age - ColorHold) / ColorFade)
            : s.TextColor;
    }
}
