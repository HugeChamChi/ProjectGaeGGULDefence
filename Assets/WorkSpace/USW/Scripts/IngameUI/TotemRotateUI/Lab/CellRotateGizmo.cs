using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 범위 칸의 투영 위치에 면과 강조 테두리를 겹치는 시안.</summary>
public sealed class CellRotateGizmo : ModernRotateGizmo
{
    private const int Capacity = 24;
    private readonly Image[] _tiles = new Image[Capacity];
    /// <inheritdoc/>
    public override string Title => "N4 범위 인쇄";
    /// <inheritdoc/>
    public override string Description => "중앙 조작 도형을 없애고 실제 적용 칸만 크림색으로 인쇄. 취소하면 기존 범위로 복귀.";
    /// <summary>재사용할 범위 면을 만든다.</summary>
    public CellRotateGizmo(RectTransform parent, TotemRotateLabSprites sprites, TMP_FontAsset font) : base(parent, sprites, font, "N4_Cell")
    {
        for (int i = 0; i < Capacity; i++)
        {
            _tiles[i] = AddImage("RangeFace" + i, Root, sprites.Frame, Vector2.one, Paper);
            _tiles[i].gameObject.SetActive(false);
        }
    }
    /// <summary>카메라 투영으로 얻은 실제 유효 범위 칸 위치를 표시한다.</summary>
    public void SetCells(IReadOnlyList<Vector2> positions, Vector2 size)
    {
        for (int i = 0; i < Capacity; i++)
        {
            bool visible = Candidate >= 0 && i < positions.Count;
            _tiles[i].gameObject.SetActive(visible);
            if (!visible) continue;
            _tiles[i].rectTransform.anchoredPosition = positions[i];
            _tiles[i].rectTransform.sizeDelta = size * .94f;
            _tiles[i].color = Paper;
        }
    }
    /// <inheritdoc/>
    protected override void OnCandidateChanged(int candidate) => State(candidate);
}
