using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 게임 그대로(TotemInteractionUI 프리팹 RotationOverlay 재현): 130px 반투명 청록 마름모 위에서
/// 42px 표시가 54px만큼 4방향으로 움직인다. 비교 기준용.
/// </summary>
public sealed class DiamondRotateGizmo : TotemRotateLabGizmo
{
    private const float DiamondSize = 130f;
    private const float IndicatorSize = 42f;
    private const float IndicatorDistance = 54f;
    private readonly RectTransform _indicator;

    /// <inheritdoc/>
    public override float DeadZone => 0f;
    /// <inheritdoc/>
    public override string Title => "현재 (마름모)";
    /// <inheritdoc/>
    public override string Description => "지금 게임 그대로. 130px 마름모 + 42px 표시가 54px 이동, 취소 반경 24 기기 픽셀(약 1.5mm), 안내 문구 없음.";
    /// <inheritdoc/>
    public override bool ShowsHint => false;
    /// <inheritdoc/>
    protected override bool AnimatesIn => false;

    /// <summary>프리팹과 같은 크기·색으로 만든다.</summary>
    public DiamondRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites) : base(parent, sprites, "Gizmo_Current")
    {
        var diamond = AddImage("Diamond", Root, null, Vector2.one * DiamondSize, new Color(0.1f, 0.35f, 0.45f, 0.38f));
        diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var indicator = AddImage("DirectionDiamond", Root, null, Vector2.one * IndicatorSize, new Color(0.35f, 0.95f, 1f, 0.8f));
        indicator.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        _indicator = indicator.rectTransform;
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate) =>
        _indicator.anchoredPosition = candidate < 0 ? Vector2.zero : Directions[candidate] * IndicatorDistance;
}
