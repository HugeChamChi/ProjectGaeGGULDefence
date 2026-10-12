/// <summary>
/// 획득 선택지 목록의 한 칸. 획득 당시 확정된 설명(snapshot)과 적용 대상 유닛을 함께 보관한다.
/// 대상은 카드 SO의 Composition.Feedback[0].Target을 쓰고, 비어 있으면 공용으로 본다.
/// </summary>
public sealed class SelectionInventoryItem
{
    /// <summary>원본 카드.</summary>
    public LevelUpData Card { get; }
    /// <summary>획득 때 확정된 수치가 들어간 설명. 없으면 SO 원문을 쓴다.</summary>
    public DescriptionSnapshot Snapshot { get; }
    /// <summary>효과가 적용되는 유닛 분류.</summary>
    public SelectionFeedbackTarget Target { get; }
    /// <summary>지속 효과 없이 획득 즉시 명령만 실행하는 카드(다시 뽑기 등).</summary>
    public bool Instant { get; }

    /// <summary>카드와 확정 설명으로 칸을 만든다. target을 주면 SO 대신 그 대상을 쓴다(실험실 보정용).</summary>
    public SelectionInventoryItem(LevelUpData card, DescriptionSnapshot snapshot, SelectionFeedbackTarget? target = null)
    {
        Card = card;
        Snapshot = snapshot;
        var composition = card != null ? card.Composition : null;
        var feedback = composition?.Feedback;
        Target = target ?? (feedback != null && feedback.Count > 0 && feedback[0] != null
            ? feedback[0].Target : SelectionFeedbackTarget.AllUnits);
        Instant = composition != null && composition.Commands != null && composition.Commands.Count > 0
                  && (composition.Effects == null || composition.Effects.Count == 0);
    }

    /// <summary>레벨업 선택지와 같은 규칙으로 설명을 만든다: 간단히/자세히 → 확정 수치 → 카드에 저작된 용어만 링크.</summary>
    public string FormatDescription(bool detailed, TermLinker linker)
    {
        if (Card == null) return string.Empty;
        string text = Snapshot != null ? DescriptionFormatter.Format(Snapshot, detailed, null, true)
            : detailed ? Card.description : Card.simpleDescription;
        return linker == null ? text : linker.AddLinks(text, Card.DescriptionTerms);
    }
}
