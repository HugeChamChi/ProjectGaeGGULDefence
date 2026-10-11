using System;

/// <summary>모든 지속 기여가 반영된 뒤 제공되는 선택 효과 변경 알림.</summary>
public interface ISelectionEffectChanges
{
    /// <summary>새 계산 결과를 읽을 수 있을 때 한 번 발행한다.</summary>
    event Action OnEffectsChanged;
}
