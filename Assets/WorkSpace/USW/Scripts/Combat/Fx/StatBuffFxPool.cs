using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>씬이 소유하는 시안별 연출 풀. 전투 드론에는 복제하지 않는다.</summary>
public sealed class StatBuffFxPool : IDisposable
{
    private readonly UnitBuffVisualSettings _settings;
    private readonly Dictionary<StatBuffFxStyle, Stack<StatBuffFx>> _available = new();
    private Transform _root;
    private StatBuffSpriteAtlas _atlas;
    /// <summary>저작된 시안 설정을 사용한다.</summary>
    public StatBuffFxPool(UnitBuffVisualSettings settings) => _settings = settings;
    /// <summary>같은 시안으로 준비된 인스턴스를 대여한다.</summary>
    public StatBuffFx Rent(StatBuffFxStyle style)
    {
        if (style == null) return null;
        _atlas ??= new StatBuffSpriteAtlas(_settings.Attack, _settings.Speed, _settings.Mixed, _settings.Disabled);
        if (_available.TryGetValue(style, out var items) && items.Count > 0)
        {
            var reused = items.Pop(); reused.gameObject.SetActive(true); return reused;
        }
        if (_root == null) _root = new GameObject("UnitBuffFxPool").transform;
        var go = new GameObject("UnitBuff_" + style.name);
        go.transform.SetParent(_root, false);
        var fx = go.AddComponent<StatBuffFx>();
        fx.ConfigureCompact(_settings.AdditiveMaterial, _atlas.Resolve);
        return fx;
    }
    /// <summary>표시를 즉시 정리하고 같은 시안 풀로 돌려준다.</summary>
    public void Return(StatBuffFx fx)
    {
        if (fx == null) return;
        var style = fx.Style;
        fx.Clear(); fx.gameObject.SetActive(false);
        if (style == null) return;
        if (!_available.TryGetValue(style, out var items)) _available.Add(style, items = new());
        items.Push(fx);
    }
    /// <summary>씬 종료 시 활성·비활성 인스턴스를 함께 제거한다.</summary>
    public void Dispose()
    {
        _available.Clear();
        if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        _root = null;
        _atlas?.Dispose(); _atlas = null;
    }
}
