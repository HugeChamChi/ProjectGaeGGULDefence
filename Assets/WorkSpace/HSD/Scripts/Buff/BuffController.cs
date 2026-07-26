using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 1체당 1개 부착되는 버프 보관/집계 컴포넌트. UnitBase.Init()에서 UnitStatsModifier 등과
/// 동일한 방식으로 부착·초기화된다.
/// </summary>
public class BuffController : MonoBehaviour
{
    private UnitBase _unit;
    private UnitDependencies _deps;

    private readonly List<BuffInstance> _active = new();
    private readonly Dictionary<StatKind, float> _cachedMultipliers = new();
    private bool _dirty = true;

    public void Init(UnitBase unit, UnitDependencies deps)
    {
        _unit = unit;
        _deps = deps;
    }

    /// <summary>버프를 적용한다. 이미 걸려있으면 스택 정책에 따라 재적용(Reapply)한다.</summary>
    public void ApplyBuff(BuffData data, UnitBase source = null)
    {
        if (data == null) return;

        var existing = _active.Find(b => b.Def == data);
        if (existing != null)
        {
            existing.Reapply(data.stackPolicy);
            NotifyStackChanged(existing);
            Debug.Log($"[Buff] {data.buffName} 스택 증가: {existing.StackCount} (대상: {name})");
        }
        else
        {
            var instance = new BuffInstance(data, source);
            _active.Add(instance);
            NotifyApply(instance);
            Debug.Log($"[Buff] {data.buffName} 최초 적용: {instance.StackCount} 스택 (대상: {name})");
        }

        _dirty = true;
    }

    public void RemoveBuff(BuffData data)
    {
        if (data == null) return;

        int index = _active.FindIndex(b => b.Def == data);
        if (index < 0) return;

        var instance = _active[index];
        _active.RemoveAt(index);
        NotifyRemove(instance);

        _dirty = true;
    }

    /// <summary>kind 스탯의 최종 배율(1f + 스택 합산)을 반환한다. 스택 변경 시에만 재계산되는 캐시.</summary>
    public float GetStatMultiplier(StatKind kind)
    {
        if (_dirty) RebuildCache();

        return _cachedMultipliers.TryGetValue(kind, out var mult) ? mult : 1f;
    }

    private void Update()
    {
        if (_active.Count == 0) return;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (!_active[i].IsExpired) continue;

            var instance = _active[i];
            _active.RemoveAt(i);
            NotifyRemove(instance);
            _dirty = true;
        }
    }

    private void RebuildCache()
    {
        _cachedMultipliers.Clear();

        var tier = _unit.currentTier;
        foreach (var instance in _active)
        {
            if (instance.Def.effects == null) continue;

            foreach (var effect in instance.Def.effects)
            {
                if (effect is StatModifierBuffEffect stat)
                {
                    _cachedMultipliers.TryGetValue(stat.kind, out var accumulated);
                    _cachedMultipliers[stat.kind] = accumulated + stat.amountPerStack.Get(tier) * instance.StackCount;
                }
            }
        }

        var kinds = new List<StatKind>(_cachedMultipliers.Keys);
        foreach (var kind in kinds)
            _cachedMultipliers[kind] = 1f + _cachedMultipliers[kind];

        _dirty = false;
    }

    private void NotifyApply(BuffInstance instance)
    {
        if (instance.Def.effects == null) return;
        foreach (var effect in instance.Def.effects)
            effect?.OnApply(instance, _unit);
    }

    private void NotifyStackChanged(BuffInstance instance)
    {
        if (instance.Def.effects == null) return;
        foreach (var effect in instance.Def.effects)
            effect?.OnStackChanged(instance, _unit);
    }

    private void NotifyRemove(BuffInstance instance)
    {
        if (instance.Def.effects == null) return;
        foreach (var effect in instance.Def.effects)
            effect?.OnRemove(instance, _unit);
    }
}
