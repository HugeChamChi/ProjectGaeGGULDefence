using UnityEngine;
using static CenterToastMath;

/// <summary>
/// B 라인 스플릿 (모던·텐션) — 영화 HUD처럼: 강조선 두 줄이 가운데에서 좌우로 쭉 뻗고,
/// 위아래로 갈라지며 그 사이에 어두운 유리 패널이 열린다. 글자는 가운데에서 바깥으로 드러나며 자간이 조여든다.
/// 연타하면 이전 것이 선으로 빠르게 닫히고 새 것이 곧바로 다시 갈라지며 열린다 (열림·닫힘 리듬, 횟수 표시 없음).
/// 사라질 땐 글자가 가운데로 닫히고, 패널이 선 사이로 접힌 뒤, 선이 가운데로 빨려 들어간다.
/// 모양은 CenterToastLinePanel (E·F와 공유).
/// </summary>
public sealed class CenterToastLineSplitMotion : ICenterToastMotion
{
    private const float FontRatio = 0.9f;
    private const float Height = 100f;
    private const float PadX = 64f;
    private const float LineGrowSeconds = 0.12f;
    private const float PanelStart = 0.05f;
    private const float PanelOpenSeconds = 0.12f;
    private const float RevealStart = 0.1f;
    private const float RevealSeconds = 0.16f;
    private const float FlashSeconds = 0.12f;        // 열리는 순간 선이 하얗게 번쩍

    /// <inheritdoc />
    public bool MergeRepeats => false;
    /// <inheritdoc />
    public bool SingleSlot => true;
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
        float line = OutExpo(e.Age / LineGrowSeconds) * (1f - InCubic((k - 0.35f) / 0.65f));
        float panel = OutExpo((e.Age - PanelStart) / PanelOpenSeconds) * (1f - OutCubic(k / 0.6f));
        float reveal = OutExpo((e.Age - RevealStart) / RevealSeconds) * (1f - Mathf.Clamp01(k / 0.45f));
        float flash = e.Age < FlashSeconds ? 1f - e.Age / FlashSeconds : 0f;

        e.Root.anchoredPosition = new Vector2(0f, s.AnchorY);
        e.Root.localScale = Vector3.one;
        e.Group.alpha = 1f;
        CenterToastLinePanel.Apply(e, s, line, panel, reveal, flash);
    }
}
