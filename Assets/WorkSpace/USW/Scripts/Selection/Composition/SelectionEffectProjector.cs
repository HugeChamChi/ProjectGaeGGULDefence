using System;
using System.Collections.Generic;

/// <summary>활성 카드에서 지속 효과만 재계산한다. 명령·이벤트·난수·게임 객체를 실행하지 않는다.</summary>
public static class SelectionEffectProjector
{
    /// <summary>외부 입력을 수정하지 않고 새 불변 결과를 계산한다.</summary>
    public static SelectionEffectSnapshot Project(IEnumerable<SelectionCardSnapshot> cards, float baseCritChance = 0f)
    {
        if (cards == null) throw new ArgumentNullException(nameof(cards));
        return new SelectionEffectSnapshot(cards, baseCritChance);
    }
}
