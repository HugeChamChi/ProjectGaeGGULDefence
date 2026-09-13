using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>TD1007: 범위 내 일반 공격의 각 발사를 지연 재현한다. 분신은 유닛으로 등록하지 않는다.</summary>
public sealed class TotemShadowAttack : RangedBuffTotemBase
{
    [Inject] private AudioManager _audioManager;
    private readonly HashSet<UnitCombatComponent> _subscribed = new();
    private readonly HashSet<UnitCombatComponent> _targets = new();
    private readonly List<UnitCombatComponent> _removed = new();
    private readonly List<PendingShot> _pending = new();
    private readonly List<ShadowAttackVisual> _visuals = new();
    private int _placementVersion;

    private sealed class PendingShot
    {
        public float Remaining;
        public BasicAttackReplay Attack;
        public Action Fire;
    }

    protected override void ApplyBuff()
    {
        _placementVersion++;
        UnitBase.OnAnyUnitChanged += RefreshTargets;
        RefreshTargets();
    }

    protected override void RemoveBuff()
    {
        _placementVersion++;
        UnitBase.OnAnyUnitChanged -= RefreshTargets;
        foreach (var combat in _subscribed)
            if (combat != null) combat.OnBasicAttackStarted -= OnAttack;
        _subscribed.Clear();
        _pending.Clear();
        foreach (var visual in _visuals) if (visual != null) visual.Stop();
    }

    /// <inheritdoc />
    public override void PaintAffectedCells()
    {
        base.PaintAffectedCells();
        RefreshTargets();
    }

    protected override void PaintRangeBuffs(GridCell cell)
    {
        foreach (var function in Data.functions) function?.Apply(this, cell, _totemBuffManager);
    }

    private void RefreshTargets()
    {
        _targets.Clear();
        if (IsActive)
            foreach (var cell in GetAffectedCells())
            {
                var unit = cell.OccupyingUnit;
                if (unit != null && unit.currentCell == cell && unit.Combat != null) _targets.Add(unit.Combat);
            }
        _removed.Clear();
        foreach (var combat in _subscribed) if (combat == null || !_targets.Contains(combat)) _removed.Add(combat);
        foreach (var combat in _removed)
        {
            if (combat != null) combat.OnBasicAttackStarted -= OnAttack;
            _subscribed.Remove(combat);
        }
        foreach (var combat in _targets)
            if (_subscribed.Add(combat)) combat.OnBasicAttackStarted += OnAttack;
    }

    private void OnAttack(BasicAttackReplay attack)
    {
        var settings = Data?.ShadowAttack;
        if (!IsActive || settings == null || float.IsNaN(settings.DelaySeconds) ||
            float.IsInfinity(settings.DelaySeconds) || settings.DelaySeconds < 0) return;
        ShadowAttackVisual available = null;
        var castEffect = attack.Source.unitData?.basicAttackData?.castEffect;
        int placementVersion = _placementVersion;
        if (!attack.Enable((fire, first) =>
        {
            if (!IsActive || placementVersion != _placementVersion) return;
            _pending.Add(new PendingShot
            {
                Remaining = settings.DelaySeconds, Attack = attack,
                Fire = () =>
                {
                    if (first && available != null)
                        castEffect?.Play(attack.Origin + settings.VisualOffset, available.transform, _audioManager);
                    fire();
                }
            });
        })) return;

        foreach (var visual in _visuals)
            if (visual != null && visual.Source == attack.Source && !visual.IsPlaying) { available = visual; break; }
        if (available == null)
        {
            available = ShadowAttackVisual.Create(attack.Source, transform);
            _visuals.Add(available);
        }
        available.Play(attack, settings);
    }

    private void Update() => Advance(Time.deltaTime);

    private void Advance(float deltaTime)
    {
        if (!IsActive || deltaTime <= 0f) return;
        // 앞에서부터 실행해 같은 프레임에 예약된 다중 발사의 적중 효과 순서를 유지한다.
        for (int i = 0; i < _pending.Count;)
        {
            var shot = _pending[i];
            shot.Remaining -= deltaTime;
            if (!shot.Attack.CanReplay) { _pending.RemoveAt(i); continue; }
            if (shot.Remaining > 0f) { i++; continue; }
            _pending.RemoveAt(i);
            shot.Fire();
        }
        for (int i = _visuals.Count - 1; i >= 0; i--)
            if (_visuals[i] == null || _visuals[i].Source == null)
            {
                if (_visuals[i] != null) Destroy(_visuals[i].gameObject);
                _visuals.RemoveAt(i);
            }
    }
}
