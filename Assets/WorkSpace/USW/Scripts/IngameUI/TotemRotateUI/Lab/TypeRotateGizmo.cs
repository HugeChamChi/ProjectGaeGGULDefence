using TMPro;
using UnityEngine;

/// <summary>방향 단어가 직접 선택 상태가 되는 타이포 시안.</summary>
public sealed class TypeRotateGizmo : ModernRotateGizmo
{
    private const float LabelRadius = 112f;
    private readonly TMP_Text[] _labels = new TMP_Text[4];
    /// <inheritdoc/>
    public override string Title => "N1 방향 활자";
    /// <inheritdoc/>
    public override string Description => "도형 대신 위·오른쪽·아래·왼쪽. 선택 단어만 굵고 크게, 범위는 보드에서 확인.";
    /// <summary>네 방향의 짧은 라벨을 만든다.</summary>
    public TypeRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites, TMP_FontAsset font) : base(parent, sprites, font, "N1_Type")
    {
        for (int i = 0; i < 4; i++)
        {
            _labels[i] = Label(Root, Names[i], Directions[i] * LabelRadius, new Vector2(115f, 65f), 28f);
        }
    }
    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate)
    {
        State(candidate);
        for (int i = 0; i < 4; i++)
        {
            _labels[i].color = i == candidate ? Accent : Paper;
            _labels[i].fontSize = i == candidate ? 43f : 24f;
            _labels[i].fontStyle = i == candidate ? FontStyles.Bold | FontStyles.Underline : FontStyles.Normal;
        }
    }
}
