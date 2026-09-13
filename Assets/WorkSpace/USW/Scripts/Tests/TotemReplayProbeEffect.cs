#if UNITY_EDITOR
using UnityEngine;

/// <summary>그림자 테스트에서 비피해 적중 효과/추가 연출의 호출 수를 확인한다.</summary>
public sealed class TotemReplayProbeEffect : IEffect, IAdditionalEffect
{
    /// <summary>적중 경로에서 호출된 횟수.</summary>
    public int Count { get; private set; }
    /// <inheritdoc />
    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition) => Count++;
}
#endif
