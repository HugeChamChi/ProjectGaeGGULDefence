using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

/// <summary>TD1001: 범위 안 유닛의 일반 공격에 반응해 실제 보조 투사체를 발사한다.</summary>
public sealed class TotemBonusProjectile : RangedBuffTotemBase
{
    [Inject] private ProjectilePool _projectiles;
    [Inject] private BossManager _bosses;
    private readonly Dictionary<UnitBase, UnityAction> _listeners = new Dictionary<UnitBase, UnityAction>();
    private readonly HashSet<UnitBase> _targets = new HashSet<UnitBase>();
    private readonly List<UnitBase> _removed = new List<UnitBase>();

    protected override void ApplyBuff()
    {
        UnitBase.OnAnyUnitChanged += RefreshTargets;
        RefreshTargets();
    }

    protected override void RemoveBuff()
    {
        UnitBase.OnAnyUnitChanged -= RefreshTargets;
        foreach (var pair in _listeners)
            if (pair.Key != null) pair.Key.onAttack?.RemoveListener(pair.Value);
        _listeners.Clear();
        _targets.Clear();
    }

    /// <inheritdoc />
    public override void PaintAffectedCells()
    {
        base.PaintAffectedCells();
        RefreshTargets();
    }

    protected override void PaintRangeBuffs(GridCell cell)
    {
        foreach (var function in Data.functions)
            function?.Apply(this, cell, _totemBuffManager);
    }

    private void RefreshTargets()
    {
        _targets.Clear();
        if (IsActive)
            foreach (var cell in GetAffectedCells())
                if (cell.OccupyingUnit != null && cell.OccupyingUnit.currentCell == cell)
                    _targets.Add(cell.OccupyingUnit);

        _removed.Clear();
        foreach (var pair in _listeners)
            if (pair.Key == null || !_targets.Contains(pair.Key)) _removed.Add(pair.Key);
        foreach (var unit in _removed)
        {
            if (unit != null) unit.onAttack?.RemoveListener(_listeners[unit]);
            _listeners.Remove(unit);
        }
        foreach (var unit in _targets)
        {
            if (_listeners.ContainsKey(unit)) continue;
            var capturedUnit = unit;
            UnityAction listener = () => OnBasicAttack(capturedUnit);
            unit.onAttack ??= new UnityEvent();
            unit.onAttack.AddListener(listener);
            _listeners.Add(unit, listener);
        }
    }

    private void OnBasicAttack(UnitBase unit)
    {
        var settings = Data?.BonusProjectile;
        var boss = _bosses != null ? _bosses.CurrentBoss : null;
        if (!IsActive || unit == null || unit.currentCell == null || settings == null ||
            _projectiles == null || boss == null || boss.IsDead || settings.AttackCoefficient <= 0f ||
            Random.value >= Mathf.Clamp01(settings.Chance)) return;

        int damage = unit.ComputeDamageFrom(unit.GetUpgradedAtk() * settings.AttackCoefficient);
        Vector3 position = boss.transform.position;
        _projectiles.Launch(unit.transform.position, position, settings.Projectile, () =>
        {
            if (boss != null && !boss.IsDead) boss.TakeDamage(damage);
        });
    }
}
