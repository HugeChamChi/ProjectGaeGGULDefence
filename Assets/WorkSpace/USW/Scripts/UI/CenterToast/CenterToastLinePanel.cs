using UnityEngine;
using static CenterToastParts;

/// <summary>
/// B 라인 스플릿 디자인 공용 — 각진 어두운 유리 패널 + 위·아래 강조선 2줄 + 가운데 고딕 글자.
/// B(LineSplit)·E(StackLines)·F(SlideLines)가 같은 모양을 쓰고 움직임만 다르다.
/// </summary>
public static class CenterToastLinePanel
{
    /// <summary>강조선 두께.</summary>
    public const float LineThickness = 3f;
    private const float RevealPad = 24f;
    private const float SpacingFrom = 12f;

    /// <summary>모양 잡기 (알림이 새로 뜰 때). 글자는 처음에 가려져 있다 (reveal 0).</summary>
    public static void Prepare(CenterToastEntry e, CenterToastSettings s, float fontRatio, float height, float padX)
    {
        Measure(e, s.FontSize * fontRatio);
        float width = e.TextWidth + padX * 2f;
        e.Width = width;
        e.Height = height;
        var accent = s.ModernAccent(e.Kind);
        Plain(e.Background, s.ModernGlass, new Vector2(width, height));
        Plain(e.LineA, accent, new Vector2(width, LineThickness));
        Plain(e.LineB, accent, new Vector2(width, LineThickness));
        e.Clip.sizeDelta = new Vector2(0f, height);
        e.Text.color = s.ModernText;
    }

    /// <summary>
    /// 한 프레임 모양. line = 선 가로 비율(가운데에서 뻗음), panel = 패널 세로 비율(선이 위아래로 갈라짐),
    /// reveal = 글자 드러남(가운데에서 바깥으로, 자간이 조여듦), flash = 선이 하얗게 번쩍.
    /// </summary>
    public static void Apply(CenterToastEntry e, CenterToastSettings s, float line, float panel, float reveal, float flash)
    {
        e.Background.rectTransform.localScale = new Vector3(1f, Mathf.Max(0f, panel), 1f);
        float lineY = e.Height * 0.5f * panel;
        var lineColor = Color.Lerp(s.ModernAccent(e.Kind), Color.white, Mathf.Clamp01(flash));
        e.LineA.color = e.LineB.color = lineColor;
        e.LineA.rectTransform.anchoredPosition = new Vector2(0f, lineY);
        e.LineB.rectTransform.anchoredPosition = new Vector2(0f, -lineY);
        e.LineA.rectTransform.localScale = e.LineB.rectTransform.localScale = new Vector3(Mathf.Max(0f, line), 1f, 1f);
        e.Clip.sizeDelta = new Vector2((e.TextWidth + RevealPad) * Mathf.Max(0f, reveal), e.Height);
        e.Text.characterSpacing = SpacingFrom * (1f - Mathf.Clamp01(reveal));
    }
}
