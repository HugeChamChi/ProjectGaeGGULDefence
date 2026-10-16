using UnityEngine;

/// <summary>
/// R 시안(5차, Codex N4 "범위 인쇄" 방향 확장): 토템 주변에 UI를 하나도 그리지 않고, 보드의 범위 칸만으로 방향을 읽게 한다.
/// 실제 그리기는 실험실이 소유한 <see cref="TotemRotateLabRangeView"/>가 맡고, 이 클래스는 시안 종류·이름·설명만 가진다.
/// </summary>
public sealed class RangeRotateGizmo : TotemRotateLabGizmo
{
    private const float Dead = 66f;
    private static readonly string[] Titles = { "R1 사선", "R2 회전 스윕", "R3 전후 비교", "R4 흐르는 사선", "R5 웨이브" };
    private static readonly string[] Descriptions =
    {
        "UI 없이 범위 칸만: 인게임 사선 셰이더만 표시. 취소하면 원래 범위가 흐려짐.",
        "R1 + 방향을 바꿀 때 외곽선이 토템을 축으로 '돌아서' 새 방향에 닿고, 그다음 사선이 채워짐. 회전이 눈에 보임.",
        "R1 + 원래 범위는 희미한 흰 외곽선으로 남김. '여기서 → 여기로'가 한 화면에.",
        "R1 + 사선이 고른 방향과 수직으로 서서 토템에서 바깥으로 흘러감. 정지 화면에서도 방향이 읽힘.",
        "R1 + 칸이 토템 가까운 칸부터 차례로 톡톡 차오름. 순서로 방향을 보여줌."
    };

    /// <summary>이 시안의 범위 연출 종류.</summary>
    public TotemRotateLabRangeView.Style Style { get; }
    /// <inheritdoc/>
    public override float DeadZone => Dead;
    /// <inheritdoc/>
    public override string Title => Titles[(int)Style];
    /// <inheritdoc/>
    public override string Description => Descriptions[(int)Style];
    /// <inheritdoc/>
    public override bool ShowsHint => false;
    /// <inheritdoc/>
    protected override bool AnimatesIn => false;

    /// <summary>빈 루트만 만든다(토템 주변 UI 없음).</summary>
    public RangeRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites, TotemRotateLabRangeView.Style style)
        : base(parent, sprites, "R_" + style)
    {
        Style = style;
    }

    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate) { }
}
