using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>
/// 선택 화면(레벨업·토템 보상) 일시정지 보조 — "전투는 멈추고 화면은 살아 있게" (사용자 결정 2026-09-30).
/// 순서: 화면이 열리면 HoldAttacks — 타이머·유닛 루프·드론 사격·보스 패턴을 owner 단위로 멈춘다.
///       짧은 1배속 유예 동안 이미 날아가던 투사체가 보스에 도착한 뒤, 각 화면이 timeScale 0 정지 + Enter를 부른다.
/// Enter: 보이는 것만 실제 시간으로 — 유닛·보스 하위 IPauseIdleVisual(Animator / 숨쉬기 트윈 / Spine) · 드론 부유 · 이펙트 파티클.
///        보스 Spine은 대기 모션일 때만 (패턴 모션 중이면 그대로 멈춤).
/// Exit: 보류·화면 둘 다 해제. 여러 화면이 겹쳐도(레벨업 → 토템 보상 등) 마지막 owner가 끝날 때만 되돌린다.
/// </summary>
public sealed class FieldPauseVisuals : IDisposable
{
    private readonly IObjectResolver _resolver;

    private readonly HashSet<object> _owners = new();
    private readonly HashSet<object> _holdOwners = new();
    private readonly List<DroneUnit> _heldDrones = new();
    private readonly List<UnitBase> _units = new();
    private readonly List<DroneUnit> _drones = new();
    private readonly List<ParticleSystem> _particles = new();
    private readonly List<IPauseIdleVisual> _bossVisuals = new();
    private readonly HashSet<UnitBase> _heldUnits = new();
    private BossBase _heldBoss;

    /// <summary>화면 살리기가 켜져 있는지 (owner가 하나 이상).</summary>
    public bool IsActive => _owners.Count > 0;

    /// <summary>새 공격 보류 중인지 — 보스 패턴 진행을 멈춘다 (BossPatternController).</summary>
    public bool AttacksHeld => _holdOwners.Count > 0;

    /// <summary>이미 전투가 정지되어 새 선택 화면에 유예를 줄 수 없는지.</summary>
    public bool MustFreezeImmediately => IsActive || Time.timeScale <= 0f;

    /// <summary>남은 연발을 버리지 않고 마지막 선택 화면이 닫힐 때까지 기다린다.</summary>
    public UniTask WaitForAttacksAsync(CancellationToken token) =>
        UniTask.WaitUntil(() => !AttacksHeld, cancellationToken: token);

    /// <summary>선택 도중 배치되거나 재사용된 유닛에도 현재 정지를 적용한다.</summary>
    public void RegisterUnit(UnitBase unit)
    {
        if (unit == null || !AttacksHeld) return;
        _heldUnits.Add(unit);
        unit.PauseLoops();
        if (IsActive)
        {
            unit.SetPauseIdle(true);
            if (!_units.Contains(unit)) _units.Add(unit);
        }
    }

    /// <summary>새 드론은 등록 시점의 owner 상태를 상속한다.</summary>
    public void RegisterDrone(DroneUnit drone)
    {
        if (drone == null) return;
        drone.SetAttackHold(AttacksHeld);
        if (AttacksHeld && !_heldDrones.Contains(drone)) _heldDrones.Add(drone);
        if (IsActive)
        {
            drone.SetPauseIdle(true);
            if (!_drones.Contains(drone)) _drones.Add(drone);
        }
    }

    /// <summary>
    /// 씬 매니저들은 쓸 때 resolver로 조회한다 — 생성자에서 받으면 BossPatternController(이 서비스를 주입받음) ↔ BossManager 사이에
    /// 순환 의존이 생겨 씬 초기화가 깨진다. DroneManager는 드론 씬에서만 등록된다.
    /// </summary>
    public FieldPauseVisuals(IObjectResolver resolver) => _resolver = resolver;

    private T Find<T>() where T : class => _resolver != null && _resolver.TryResolve<T>(out var value) ? value : null;

    /// <summary>owner의 선택 화면이 열렸다 — 정지 전 유예부터 새 공격(드론 사격·보스 패턴)을 막는다.</summary>
    public void HoldAttacks(object owner)
    {
        if (owner == null || !_holdOwners.Add(owner) || _holdOwners.Count > 1) return;
        Find<TimerController>()?.StopTimer();
        var grid = Find<GridManager>();
        if (grid != null)
            foreach (var cell in grid.GetOccupiedCells()) RegisterUnit(cell.OccupyingUnit);
        _heldBoss = Find<BossManager>()?.CurrentBoss;
        _heldBoss?.SetPatternHold(true);
        var drones = Find<DroneManager>();
        if (drones != null)
            foreach (var drone in drones.Drones)
            {
                if (drone == null) continue;
                RegisterDrone(drone);
            }
    }

    /// <summary>owner의 선택 화면이 완전히 멈췄다. 첫 owner일 때 화면을 실제 시간으로 돌린다.</summary>
    public void Enter(object owner)
    {
        if (owner == null || !_owners.Add(owner) || _owners.Count > 1) return;
        Apply();
    }

    /// <summary>owner의 선택 화면이 닫혔다 (보류·화면 모두). 남은 owner가 없으면 원래대로 되돌린다.</summary>
    public void Exit(object owner, bool resumeCombat = true)
    {
        if (owner == null) return;
        if (_holdOwners.Remove(owner) && _holdOwners.Count == 0)
        {
            foreach (var drone in _heldDrones) if (drone != null) drone.SetAttackHold(false);
            _heldDrones.Clear();
            _heldBoss?.SetPatternHold(false);
            _heldBoss = null;
            if (resumeCombat)
            {
                var boss = Find<BossManager>()?.CurrentBoss;
                if (boss != null && !boss.IsDead) Find<TimerController>()?.ResumeTimer();
                foreach (var unit in _heldUnits) if (unit != null && unit.currentCell != null) unit.ResumeLoops();
            }
            _heldUnits.Clear();
        }
        if (_owners.Remove(owner) && _owners.Count == 0) Restore();
    }

    /// <summary>씬 수명이 끝나면 살아 있는 외형의 시계를 복원한다.</summary>
    public void Dispose()
    {
        foreach (var drone in _heldDrones) if (drone != null) drone.SetAttackHold(false);
        _heldBoss?.SetPatternHold(false);
        _holdOwners.Clear();
        _owners.Clear();
        _heldDrones.Clear();
        _heldUnits.Clear();
        Restore();
    }

    private void Apply()
    {
        var grid = Find<GridManager>();
        if (grid != null)
            foreach (var cell in grid.GetOccupiedCells())
            {
                var unit = cell.OccupyingUnit;
                if (unit == null) continue;
                unit.SetPauseIdle(true);
                _units.Add(unit);
            }

        var drones = Find<DroneManager>();
        if (drones != null)
            foreach (var drone in drones.Drones)
            {
                if (drone == null) continue;
                drone.SetPauseIdle(true);
                _drones.Add(drone);
            }

        var bossManager = Find<BossManager>();
        var boss = bossManager != null ? bossManager.CurrentBoss : null;
        if (boss != null)
        {
            boss.GetComponentsInChildren(true, _bossVisuals);
            foreach (var visual in _bossVisuals) visual.SetPauseIdle(true);
        }

        // 화면에 떠 있는 이펙트만 (열릴 때 한 번 조회 — 매 프레임 검색 아님). 원래 unscaled인 UI 파티클은 건드리지 않는다.
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var main = ps.main;
            if (main.useUnscaledTime) continue;
            main.useUnscaledTime = true;
            _particles.Add(ps);
        }
    }

    private void Restore()
    {
        foreach (var unit in _units) if (unit != null) unit.SetPauseIdle(false);
        foreach (var drone in _drones) if (drone != null) drone.SetPauseIdle(false);
        foreach (var visual in _bossVisuals) if (visual is Object o && o != null) visual.SetPauseIdle(false);
        foreach (var ps in _particles)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.useUnscaledTime = false;
        }
        _units.Clear();
        _drones.Clear();
        _particles.Clear();
        _bossVisuals.Clear();
    }
}
