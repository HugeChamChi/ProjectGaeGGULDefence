using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>보스별 쿨타임과 우선순위를 관리하며 한 보스는 한 패턴만 시전한다.</summary>
[DefaultExecutionOrder(32000)]
public class BossPatternController : MonoBehaviour
{
    private const float MinimumInterval = 0.01f;
    [Inject] private GridManager _gridManager;
    [Inject] private GameManager _gameManager;
    private BattleCameraShake _shake;

    private sealed class PatternState
    {
        public BossPatternData Data;
        public float Remaining;
        public bool PhaseFired, PhaseReady;
        public bool Timed => Data.triggerType != PatternTriggerType.Phase;
    }
    private sealed class BossPatternEntry
    {
        public readonly List<PatternState> Patterns = new();
        public BossPatternData Casting;
        public float Elapsed, Duration, ImpactTime, StunDuration, ShakeDuration, ShakeIntensity;
        public StunVisualSettings Visual;
        public bool ImpactApplied, UseShake;
        public int PendingImpactFrame = -1;
        public Action<decimal, decimal> HpHandler;
    }
    private readonly Dictionary<BossBase, BossPatternEntry> _entries = new();
    private readonly List<BossBase> _iteration = new();

    private void Start()
    {
        var camera = Camera.main;
        if (camera != null) _shake = camera.GetComponent<BattleCameraShake>() ?? camera.gameObject.AddComponent<BattleCameraShake>();
    }

    /// <summary>전투 시작/보스 스폰 시 최초 쿨타임을 부여한다.</summary>
    public void RegisterBoss(BossBase boss, BossPatternData[] patterns)
    {
        if (boss == null) return;
        UnregisterBoss(boss);
        var entry = new BossPatternEntry();
        if (patterns != null)
            foreach (var pattern in patterns)
                if (pattern != null && !entry.Patterns.Exists(p => p.Data == pattern))
                    entry.Patterns.Add(new PatternState { Data = pattern, Remaining = pattern.triggerType == PatternTriggerType.Phase ? 0f : Mathf.Max(MinimumInterval, pattern.interval) });
        entry.HpHandler = (current, maximum) =>
        {
            if (current <= 0) { ResetCast(entry); return; }
            foreach (var state in entry.Patterns)
            {
                if (state.PhaseFired || state.Data.triggerType == PatternTriggerType.Timer) continue;
                if (maximum > 0 && current / maximum <= (decimal)state.Data.hpThreshold)
                { state.PhaseFired = true; state.PhaseReady = true; state.Remaining = 0f; }
            }
        };
        boss.OnHpChanged += entry.HpHandler;
        _entries.Add(boss, entry);
    }

    /// <summary>개별 보스의 대기 패턴과 시전을 해제한다.</summary>
    public void UnregisterBoss(BossBase boss)
    {
        if (ReferenceEquals(boss, null) || !_entries.TryGetValue(boss, out var entry)) return;
        if (boss != null) boss.OnHpChanged -= entry.HpHandler;
        ResetCast(entry);
        _entries.Remove(boss);
    }

    /// <summary>웨이브 전환/전투 종료 시 패턴, 셀 효과와 스턴을 정리한다.</summary>
    public void UnregisterAll()
    {
        foreach (var pair in _entries)
            if (pair.Key != null) pair.Key.OnHpChanged -= pair.Value.HpHandler;
        _entries.Clear();
        _shake?.Stop();
        if (_gridManager == null) return;
        _gridManager.SetBossTelegraphAll(false);
        foreach (var cell in _gridManager.AllCells())
        {
            if (cell == null) continue;
            cell.Model.ClearBossDebuffs();
            cell.OccupyingUnit?.ClearStun();
        }
    }

    private void LateUpdate()
    {
        if (_gameManager != null && (_gameManager.CurrentState == GameManager.GameState.Win || _gameManager.CurrentState == GameManager.GameState.Lose))
        { if (_entries.Count > 0) UnregisterAll(); return; }
        if (Time.deltaTime <= 0f || (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing)) return;
        _iteration.Clear();
        _iteration.AddRange(_entries.Keys);
        foreach (var boss in _iteration)
        {
            if (boss == null || boss.IsDead) { UnregisterBoss(boss); continue; }
            if (!_entries.TryGetValue(boss, out var entry)) continue;
            try { Tick(boss, entry, Time.deltaTime); }
            catch (Exception error) { ResetCast(entry); Debug.LogException(error, boss); }
        }
    }

    private void Tick(BossBase boss, BossPatternEntry entry, float deltaTime)
    {
        foreach (var state in entry.Patterns)
            if (state.Timed || state.Remaining > 0f) state.Remaining = Mathf.Max(0f, state.Remaining - deltaTime);
        if (entry.Casting != null)
        {
            entry.Elapsed += deltaTime;
            if (entry.Casting.patternType == BossPatternType.Earthquake && !entry.ImpactApplied && entry.Elapsed >= entry.ImpactTime)
            {
                // 판정 도래 프레임의 모든 피해/전투 종료 처리를 먼저 끝낸다.
                if (entry.PendingImpactFrame < 0) entry.PendingImpactFrame = Time.frameCount;
                else if (Time.frameCount > entry.PendingImpactFrame)
                {
                    entry.ImpactApplied = true;
                    ApplyEarthquake(entry);
                }
            }
            if (entry.Elapsed < entry.Duration || (entry.Casting.patternType == BossPatternType.Earthquake && !entry.ImpactApplied)) return;
            ResetCast(entry);
        }

        PatternState next = null;
        foreach (var state in entry.Patterns)
            if (state.Remaining <= 0f && (state.Timed || state.PhaseReady)
                && (next == null || state.Data.Priority > next.Data.Priority)) next = state;
        if (next == null) return;
        var data = next.Data;
        if (data.patternType == BossPatternType.Earthquake && !HasTarget())
        { next.Remaining = Mathf.Max(MinimumInterval, data.RetryDelay); return; }
        next.Remaining = Mathf.Max(MinimumInterval, data.interval);
        next.PhaseReady = false;
        entry.Casting = data;
        entry.Duration = Mathf.Max(data.SkillDuration, data.patternType == BossPatternType.Earthquake ? data.ImpactTimeSec : 0f);
        entry.ImpactTime = Mathf.Max(0f, data.ImpactTimeSec);
        entry.StunDuration = Mathf.Max(0f, data.duration);
        entry.Visual = data.StunVisual;
        entry.UseShake = data.UseScreenShake;
        entry.ShakeDuration = data.ShakeDuration;
        entry.ShakeIntensity = data.ShakeIntensity;
        boss.PlayPatternAnimation(data);
        if (data.patternType == BossPatternType.Earthquake) _gridManager?.SetBossTelegraphAll(true);
        else boss.ExecutePattern(data);
    }

    private bool HasTarget()
    {
        if (_gridManager == null) return false;
        foreach (var cell in _gridManager.AllCells())
            if (IsValid(cell?.OccupyingUnit)) return true;
        return false;
    }
    private static bool IsValid(UnitBase unit) => unit != null && unit.isActiveAndEnabled && unit.currentCell != null;
    private void ApplyEarthquake(BossPatternEntry entry)
    {
        if (_gridManager == null) return;
        _gridManager.SetBossTelegraphAll(false);
        foreach (var cell in _gridManager.AllCells())
        {
            var unit = cell?.OccupyingUnit;
            if (IsValid(unit)) unit.ApplyStun(entry.StunDuration, entry.Visual);
        }
        if (entry.UseShake)
        {
            try { _shake?.Play(entry.ShakeDuration, entry.ShakeIntensity); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
    }
    private static void ResetCast(BossPatternEntry entry)
    {
        entry.Casting = null;
        entry.Visual = null;
        entry.Elapsed = 0f;
        entry.ImpactApplied = false;
        entry.PendingImpactFrame = -1;
    }
    private void OnDestroy() => UnregisterAll();
}
