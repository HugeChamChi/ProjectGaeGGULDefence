using System;
using UnityEngine;

/// <summary>실험실 전용 스킬 복사본에 추가하는 시각 효과. 기존 피해 처리 뒤 실제 적중 시 호출된다.</summary>
[Serializable]
public sealed class BossHitReactionImpact : IEffect
{
    [NonSerialized] private BossHitReactionLab _lab;

    /// <summary>현재 테스트 씬의 반응 수신자를 지정한다.</summary>
    public BossHitReactionImpact(BossHitReactionLab lab) => _lab = lab;

    /// <inheritdoc />
    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        if (_lab != null && caster is Disigman && target != null && !target.IsDead)
            _lab.OnHeavyImpact(target,hitPosition);
    }
}
