using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>조합형 카드의 저작/이관 DTO. 런타임에서 직접 보관하지 않고 확정 snapshot으로 변환해야 한다.</summary>
[Serializable]
public sealed class SelectionCardDefinition
{
    /// <summary>유효 런 풀에서 함께 등록할 수 없는 기능 그룹. 카드 ID와 구분한다.</summary>
    public List<string> ExclusiveGroups = new();
    /// <summary>지속 효과 정의. 새 schema는 빈 목록이어도 legacy로 fallback하지 않는다.</summary>
    [SerializeReference, SelectableReference] public List<SelectionEffectDefinition> Effects = new();
    /// <summary>획득/필드보기 연출 대상. 빈 목록은 기본 수집 지점만 뜻한다.</summary>
    public List<SelectionFeedbackDefinition> Feedback = new();
    /// <summary>획득 시 실행할 명령. 지속 효과 계산에서는 실행하지 않는다.</summary>
    [SerializeReference, SelectableReference] public List<SelectionCommandDefinition> Commands = new();
}
