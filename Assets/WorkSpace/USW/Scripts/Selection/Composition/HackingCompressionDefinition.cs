using System;

/// <summary>델탕의 기본 충전량을 늘리고 고정 추가분을 포함한 해킹 생산량을 배가한다.</summary>
[Serializable]
public sealed class HackingCompressionDefinition : SelectionEffectDefinition
{
    /// <summary>마나 회복/감소 카드 적용 전의 기본 완충량.</summary>
    public int RequiredAttacks;
    /// <summary>등급 생산량과 고정 추가 생산량의 배수.</summary>
    public int ProductionMultiplier;
}
