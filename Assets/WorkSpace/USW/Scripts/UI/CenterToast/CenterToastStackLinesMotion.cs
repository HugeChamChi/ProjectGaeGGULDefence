using UnityEngine;
using static CenterToastMath;

/// <summary>
/// E = B 디자인 + A 연출 — 줄마다 B의 라인 패널(각진 유리 + 위·아래 강조선)이 열리고,
/// 연타하면 새 줄이 아래에서 스냅으로 치고 올라오며 이전 줄들은 위로 밀리며 작아지고 흐려진다 (참고 GIF처럼 쌓임).
/// 각 줄은 B처럼 선이 가운데서 뻗고 → 위아래로 갈라지며 패널이 열리고 → 글자가 가운데서 드러난다.
/// 사라질 땐 B처럼 선 사이로 접히며 흐려진다.
/// </summary>
public sealed class CenterToastStackLinesMotion : ICenterToastMotion
{
    private const float FontRatio = 0.84f;
    private const float Height = 80f;
    private const float PadX = 52f;
    private const float LineGrowSeconds = 0.1f;
    private const float PanelStart = 0.04f;
    private const float PanelOpenSeconds = 0.1f;
    private const float RevealStart = 0.08f;
    private const float RevealSeconds = 0.14f;
    private const float FlashSeconds = 0.12f;
    private const float EnterSeconds = 0.18f;
    private const float EnterDrop = 34f;
    private const float OlderScale = 0.94f;      // 한 줄 위로 갈 때마다 크기 배율
    private const float OlderAlpha = 0.6f;       // 한 줄 위로 갈 때마다 투명도 배율

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
        => CenterToastLinePanel.Prepare(e, s, FontRatio, Height, PadX);

    /// <inheritdoc />
    public void Render(CenterToastEntry e, int index, CenterToastSettings s)
    {
        float k = e.ExitProgress(ExitSeconds);
        float enter = OutExpo(e.Age / EnterSeconds);
        float older = Mathf.Max(0f, e.Slot);
        float step = Height + s.StackGap;
        float sc = Mathf.Pow(OlderScale, older);

        e.Root.anchoredPosition = new Vector2(0f, s.AnchorY + e.Slot * step - EnterDrop * (1f - enter));
        e.Root.localScale = new Vector3(sc, sc, 1f);
        e.Group.alpha = Mathf.Pow(OlderAlpha, older) * (1f - InCubic(k));

        float line = OutExpo(e.Age / LineGrowSeconds) * (1f - InCubic((k - 0.35f) / 0.65f));
        float panel = OutExpo((e.Age - PanelStart) / PanelOpenSeconds) * (1f - OutCubic(k / 0.6f));
        float reveal = OutExpo((e.Age - RevealStart) / RevealSeconds) * (1f - Mathf.Clamp01(k / 0.45f));
        float flash = e.Age < FlashSeconds ? 1f - e.Age / FlashSeconds : 0f;
        CenterToastLinePanel.Apply(e, s, line, panel, reveal, flash);
    }
}
