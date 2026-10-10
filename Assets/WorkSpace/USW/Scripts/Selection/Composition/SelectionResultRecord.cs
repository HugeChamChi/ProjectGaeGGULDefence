using UnityEngine;

/// <summary>획득 순간의 결과 화면 표시 기록. 이후 formatter나 공유 SO 변경과 독립적이다.</summary>
public sealed class SelectionResultRecord
{
    /// <summary>획득 카드 식별자.</summary>
    public int CardId { get; }
    /// <summary>획득 당시 아이콘.</summary>
    public Sprite Icon { get; }
    /// <summary>획득 당시 이름.</summary>
    public string Name { get; }
    /// <summary>획득 당시 확정 문구.</summary>
    public string Description { get; }
    internal SelectionResultRecord(SelectionCardSnapshot card, string description)
    { CardId=card.CardId; Icon=card.Icon; Name=card.Name; Description=description ?? card.DescriptionTemplate ?? string.Empty; }
}
