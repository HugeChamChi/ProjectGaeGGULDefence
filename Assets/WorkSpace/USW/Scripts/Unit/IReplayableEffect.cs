using System;
using UnityEngine;

/// <summary>원본 적중 시 계산한 결과를 그림자 적중에도 재사용할 수 있는 효과.</summary>
public interface IReplayableEffect : IEffect
{
    /// <summary>치명타 등 캐스터 계산을 한 번만 수행하고 재사용 가능한 적용 동작을 반환한다.</summary>
    Action<BossBase, Vector3> Capture(UnitBase caster);
}
