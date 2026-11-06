using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

/// <summary>확정된 시간 변경에만 반응하며 시간 변경이나 스킬 충전을 재발생시키지 않는다.</summary>
public sealed class GlitchReactionBinding : IStartable, IDisposable
{
    private readonly TimerController _timer;
    private readonly GameManager _game;
    private readonly DroneManager _drones;
    private readonly BossManager _bosses;
    private readonly LevelUpRunState _state;
    private readonly IGlitchEffectReader _effects;
    private readonly Dictionary<UnitBase, (BossBase Target, long RunId, SelfDestructDrone Prefab)> _pendingMergeBombs = new();

    /// <summary>드론 씬의 런 수명으로 주입한다.</summary>
    public GlitchReactionBinding(TimerController timer, GameManager game, DroneManager drones, BossManager bosses,
        LevelUpRunState state, IGlitchEffectReader effects)
    { _timer = timer; _game = game; _drones = drones; _bosses = bosses; _state = state; _effects = effects; }

    /// <inheritdoc />
    public void Start()
    {
        _timer.OnGlitch += React;
        UnitBase.OnAnyUnitChanged += FlushPendingMergeBombs;
    }
    /// <inheritdoc />
    public void Dispose()
    {
        _timer.OnGlitch -= React;
        UnitBase.OnAnyUnitChanged -= FlushPendingMergeBombs;
        _pendingMergeBombs.Clear();
    }

    private void React(UnitBase actor, BossBase target, double actualSeconds, bool disigmanRecovery)
    {
        if (_game != null && _game.IsFinished) return;
        _state.RecordGlitch();
        if (actor == null || target == null || target.IsDead || _bosses.CurrentBoss != target) return;
        if (!actor.isActiveAndEnabled || actor.currentCell == null)
        {
            // Merge time is already paid; only its source-unit bomb waits for placement.
            if (actualSeconds < 0 && _effects.GlitchBombPrefab != null)
                _pendingMergeBombs[actor] = (target, _state.RunId, _effects.GlitchBombPrefab);
            return;
        }

        if (disigmanRecovery && actor is Disigman && actualSeconds > 0
            && _effects.RecoveryHackingStacks > 0 && _effects.RecoveryHackingData != null)
        {
            var hacking = _drones.Hacking;
            hacking.Configure(_effects.RecoveryHackingData);
            hacking.Add(target, _effects.RecoveryHackingStacks);
        }

        LaunchBomb(actor, target, _effects.GlitchBombPrefab);
    }

    private void FlushPendingMergeBombs()
    {
        if (_pendingMergeBombs.Count == 0) return;
        foreach (var actor in new List<UnitBase>(_pendingMergeBombs.Keys))
        {
            var pending = _pendingMergeBombs[actor];
            if (actor == null || _game?.IsFinished == true || pending.RunId != _state.RunId
                || pending.Target == null || pending.Target.IsDead || _bosses.CurrentBoss != pending.Target)
            { _pendingMergeBombs.Remove(actor); continue; }
            if (!actor.isActiveAndEnabled || actor.currentCell == null) continue;
            _pendingMergeBombs.Remove(actor);
            LaunchBomb(actor, pending.Target, pending.Prefab);
        }
    }

    private static void LaunchBomb(UnitBase actor, BossBase target, SelfDestructDrone prefab)
    {
        if (prefab == null || actor.unitData == null) return;
        var go = RM.Instantiate(prefab.gameObject, actor.transform.position, Quaternion.identity, null, true);
        if (go == null) return;
        var bomb = go.GetComponent<SelfDestructDrone>();
        if (bomb == null) { RM.Destroy(go); return; }
        int damage = actor.ComputeAttackDamageFrom(actor.GetUpgradedAtk(), 1f, out bool critical);
        bomb.Initialize(damage, critical, target, actor.unitData);
    }
}
