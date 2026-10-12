using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>실제 셀 적용 지점의 기록. 재계산 중에는 공개하지 않고 완료된 기여만 게시한다.</summary>
public sealed class TotemBuffVisualState
{
    /// <summary>같은 출처 안 이동과 단순 수치 갱신을 신규 획득과 구분하는 키.</summary>
    public readonly struct Key : IEquatable<Key>
    {
        private readonly int _source, _slot, _epoch, _kind;
        internal Key(int source, int slot, int epoch, int kind)
        { _source = source; _slot = slot; _epoch = epoch; _kind = kind; }
        public bool Equals(Key other) => _source == other._source && _slot == other._slot && _epoch == other._epoch && _kind == other._kind;
        public override bool Equals(object obj) => obj is Key other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_source, _slot, _epoch, _kind);
    }

    /// <summary>수치와 시간제 조건의 실제 종료 시점. EndEncounterSeconds가 음수면 영구 효과.</summary>
    public readonly struct Contribution
    {
        public readonly Key Source;
        public readonly StatKind Kind;
        public readonly float Amount;
        public readonly bool Disabled, Replaces;
        public readonly float EndEncounterSeconds;
        internal Contribution(Key source, StatKind kind, float amount, bool disabled, bool replaces, float end)
        { Source = source; Kind = kind; Amount = amount; Disabled = disabled; Replaces = replaces; EndEncounterSeconds = end; }
    }

    private Dictionary<GridCellModel, List<Contribution>> _published = new();
    private Dictionary<GridCellModel, List<Contribution>> _pending = new();
    private readonly List<(GridCellModel Model, Action<StatKind, float, bool, bool> Handler)> _subscriptions = new();
    private readonly Dictionary<(TotemBase Source, StatKind Kind), float> _globals = new();
    private float _pendingGlobalAttack, _pendingGlobalSpeed;
    /// <summary>선택지·연구를 제외하고 실제 토템 출처만 합산한 전역 공격력.</summary>
    public float GlobalAttackBonus { get; private set; }
    /// <summary>실제 전역 토템 출처만 합산한 공격 빈도 상승값.</summary>
    public float GlobalSpeedBonus { get; private set; }
    private TotemBase _source;
    private ITotemFunction _function;
    private int _bossEpoch;
    /// <summary>모든 셀의 재계산이 정상 완료된 후에만 발행한다.</summary>
    public event Action OnCompleted;
    /// <summary>보스의 새 시간제 효과 회차를 시작한다.</summary>
    public void BeginEncounter() => _bossEpoch++;
    /// <summary>실제 적용에 사용 중인 기능 슬롯을 지정한다. 조건을 다시 판정하지 않는다.</summary>
    public void SetFunction(ITotemFunction function) => _function = function;

    /// <summary>기존 전역 토템 API의 실제 적용/해제를 출처와 함께 기록한다.</summary>
    public void RecordGlobal(TotemBase source, StatKind kind, float delta)
    {
        if (source == null) return;
        var key = (source, kind);
        _globals.TryGetValue(key, out float amount);
        amount += delta;
        if (Mathf.Approximately(amount, 0f)) _globals.Remove(key);
        else _globals[key] = amount;
    }

    /// <summary>완료된 셀 기여를 조회한다.</summary>
    public IReadOnlyList<Contribution> Read(GridCellModel model) => model != null && _published.TryGetValue(model, out var list)
        ? list : Array.Empty<Contribution>();

    /// <summary>초기화가 끝난 셀에 실제 적용 기록만 구독한다.</summary>
    public void Begin(IEnumerable<GridCell> cells, float efficiency)
    {
        _pending.Clear();
        _pendingGlobalAttack = _pendingGlobalSpeed = 0f;
        foreach (var global in _globals)
        {
            if (global.Key.Source == null || !global.Key.Source.IsActive) continue;
            float amount = global.Value * efficiency;
            if (global.Key.Kind == StatKind.AttackPercent) _pendingGlobalAttack += amount;
            else if (global.Key.Kind == StatKind.Speed) _pendingGlobalSpeed += amount;
            SetSource(global.Key.Source);
            foreach (var cell in cells) if (cell != null) Record(cell.Model, global.Key.Kind, amount, false, false);
        }
        SetSource(null);
        foreach (var cell in cells)
        {
            if (cell == null) continue;
            var model = cell.Model;
            Action<StatKind, float, bool, bool> handler = (kind, amount, disabled, replaces) => Record(model, kind, amount, disabled, replaces);
            model.OnTotemEffectApplied += handler;
            _subscriptions.Add((model, handler));
        }
    }

    /// <summary>토템별 실제 PaintAffectedCells 호출 경계를 지정한다.</summary>
    public void SetSource(TotemBase source) { _source = source; _function = null; }

    private void Record(GridCellModel model, StatKind kind, float amount, bool disabled, bool replaces)
    {
        if (_source == null || (!disabled && kind != StatKind.AttackPercent && kind != StatKind.Speed)) return;
        if (!_pending.TryGetValue(model, out var list)) _pending.Add(model, list = new());
        if (replaces) list.RemoveAll(c => c.Replaces && c.Kind == kind && c.Disabled == disabled);
        if (!disabled && Mathf.Approximately(amount, 0f)) return;
        var window = (_function as ConditionalBuffFunction)?.condition as BossEncounterWindowCondition;
        int epoch = window != null ? _bossEpoch : _source is TotemKillRangeGrowth growth ? growth.StageIndex : 0;
        var key = new Key(_source.GetInstanceID(), _function == null ? 0 : RuntimeHelpers.GetHashCode(_function), epoch, disabled ? -1 : (int)kind);
        list.Add(new Contribution(key, kind, amount, disabled, replaces, window?.seconds ?? -1f));
    }

    /// <summary>구독을 해제하고 성공한 재계산만 원자적으로 게시한다.</summary>
    public void End(bool completed)
    {
        foreach (var subscription in _subscriptions) subscription.Model.OnTotemEffectApplied -= subscription.Handler;
        _subscriptions.Clear(); _source = null; _function = null;
        if (!completed) { _pending.Clear(); return; }
        (_published, _pending) = (_pending, _published);
        GlobalAttackBonus = _pendingGlobalAttack; GlobalSpeedBonus = _pendingGlobalSpeed;
        OnCompleted?.Invoke();
    }
}
