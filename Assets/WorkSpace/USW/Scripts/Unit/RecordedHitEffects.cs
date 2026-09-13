using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>투사체 한 발의 원본 적중 결과. 수치 계산은 첫 적중 한 번만 수행한다.</summary>
public sealed class RecordedHitEffects
{
    private readonly UnitBase _caster;
    private readonly IEffect[] _effects;
    private readonly IAdditionalEffect[] _additional;
    private readonly Action<BossBase, Vector3>[] _captured;

    /// <summary>목록의 구성은 발사 시 고정하되 원본의 기존 적중 시점 계산을 유지한다.</summary>
    public RecordedHitEffects(UnitBase caster, List<IEffect> effects, List<IAdditionalEffect> additional)
    {
        _caster = caster;
        _effects = effects?.ToArray() ?? Array.Empty<IEffect>();
        _additional = additional?.ToArray() ?? Array.Empty<IAdditionalEffect>();
        _captured = new Action<BossBase, Vector3>[_effects.Length];
    }

    /// <summary>기록 가능한 피해는 그대로 재사용하고 나머지 적중 효과/연출은 다시 적용한다.</summary>
    public void Apply(BossBase target, Vector3 position)
    {
        if (_caster == null || target == null || target.IsDead) return;
        for (int i = 0; i < _effects.Length; i++)
        {
            if (_effects[i] is IReplayableEffect replayable)
            {
                _captured[i] ??= replayable.Capture(_caster);
                _captured[i]?.Invoke(target, position);
            }
            else _effects[i]?.Apply(_caster, target, position);
        }
        foreach (var effect in _additional) effect?.Apply(_caster, target, position);
    }
}
