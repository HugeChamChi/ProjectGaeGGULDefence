using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>
/// 보스별 쿨타임과 우선순위를 관리하며 한 보스는 한 패턴만 시전한다.
/// 예고(텔레그래프)가 있는 패턴은 판정 전에 족장 액티브 스킬이 발동되면 취소된다 (카운터).
/// </summary>
[DefaultExecutionOrder(32000)]
public class BossPatternController : MonoBehaviour
{
    private const float MinimumInterval = 0.01f;
    [Inject] private GridManager _gridManager;
    [Inject] private GameManager _gameManager;
    [Inject] private FieldPauseVisuals _fieldPause;
    private BattleCameraShake _shake;

    private sealed class PatternState
    {
        public BossPatternData Data;
        public float Remaining;
        public bool PhaseFired, PhaseReady;
        public bool Timed => Data is not PlacementBossPatternData && Data.triggerType != PatternTriggerType.Phase;
    }
    private sealed class PlacementPhaseState
    {
        public PlacementBossPhaseData Data;
        public bool[] Fired;
        public readonly List<PatternState> Skills = new();
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
        public IBossPatternRuntime Runtime;
        public PatternState Requested;
        public PatternLabDriver Placement;
        public BossSpineAnimator SpineAnimator;
        public readonly List<PlacementPhaseState> PhaseGroups = new();
        public readonly Queue<PatternState> PhaseQueue = new();
    }
    private readonly Dictionary<BossBase, BossPatternEntry> _entries = new();
    private readonly List<BossBase> _iteration = new();

    /// <summary>족장 스킬로 예고 패턴이 취소됐을 때 (보스, 취소된 패턴). 카운터 연출 연결용.</summary>
    public event Action<BossBase, BossPatternData> OnPatternCountered;

    /// <summary>Whether a cast or explicit request is still in progress.</summary>
    public bool IsCastingPattern(BossBase boss) => boss != null && _entries.TryGetValue(boss, out var entry)
        && (entry.Casting != null || entry.Requested != null);


    /// <summary>시전 중이 아닐 때 등록된 패턴을 다음 전투 틱에 요청한다. 저작용 전투 씬에서도 같은 경로를 쓴다.</summary>
    public bool TryRequestPattern(BossBase boss, int index)
    {
        if (boss == null || boss.IsDead || !_entries.TryGetValue(boss, out var entry) || entry.Casting != null
            || index < 0 || index >= entry.Patterns.Count) return false;
        entry.Requested = entry.Patterns[index];
        return true;
    }

    /// <summary>Explicit scene wiring for isolated authored combat previews; production uses the same injected services.</summary>
    public void ConfigureScene(GridManager grid, GameManager game = null, FieldPauseVisuals pause = null)
    {
        _gridManager = grid;
        _gameManager = game;
        _fieldPause = pause;
    }

    /// <summary>보스가 첫 판정 전이며 족장 스킬로 끊을 수 있는 예고를 시전 중인지 여부.</summary>
    public bool IsCounterablePattern(BossBase boss) => boss != null && _entries.TryGetValue(boss, out var entry) && IsCounterable(entry);

    private void Start()
    {
        var camera = Camera.main;
        if (camera != null) _shake = camera.GetComponent<BattleCameraShake>() ?? camera.gameObject.AddComponent<BattleCameraShake>();
        ChiefActiveSkillSignals.OnActivated += HandleChiefSkillActivated;
    }

    private void HandleChiefSkillActivated() => TryCounterTelegraphedPatterns();

    /// <summary>
    /// 첫 판정 전인 지진·범위 예고 패턴을 취소한다. 시전 시점에 소비한 쿨타임은 유지한다.
    /// </summary>
    /// <returns>하나 이상 취소했으면 true.</returns>
    public bool TryCounterTelegraphedPatterns()
    {
        bool countered = false;
        _iteration.Clear();
        _iteration.AddRange(_entries.Keys);
        foreach (var boss in _iteration)
        {
            if (boss == null || !_entries.TryGetValue(boss, out var entry) || !IsCounterable(entry)) continue;
            var pattern = entry.Casting;
            entry.Placement?.TryCounter();
            entry.Placement?.ResetAll();
            entry.SpineAnimator?.ReturnToIdle();
            ResetCast(entry);
            boss.InterruptPatternAnimation(pattern);
            countered = true;
            Debug.Log($"[BossPattern] 족장 스킬 카운터: {boss.name} 패턴 취소 ({pattern.name})");
            OnPatternCountered?.Invoke(boss, pattern);
        }
        if (countered && !AnyTelegraphActive()) _gridManager?.SetBossTelegraphAll(false);
        return countered;
    }

    /// <summary>예고 중이고 아직 판정(Impact)이 오지 않은 시전.</summary>
    private static bool IsCounterable(BossPatternEntry entry) =>
        entry.Casting is PlacementBossPatternData ? entry.Placement != null && entry.Placement.IsCounterable :
        entry.Casting != null && entry.Casting.patternType == BossPatternType.Earthquake &&
        !entry.ImpactApplied && entry.PendingImpactFrame < 0;

    private bool AnyTelegraphActive()
    {
        foreach (var entry in _entries.Values)
            if (IsCounterable(entry)) return true;
        return false;
    }

    /// <summary>전투 시작/보스 스폰 시 최초 쿨타임을 부여한다.</summary>
    public void RegisterBoss(BossBase boss, BossPatternData[] patterns)
    {
        if (boss == null) return;
        UnregisterBoss(boss);
        var entry = new BossPatternEntry
        {
            Runtime = boss as IBossPatternRuntime
        };
        entry.SpineAnimator = boss.GetComponent<BossSpineAnimator>();
        entry.Runtime?.ConfigurePatternContext(_gridManager);
        if (patterns != null)
            foreach (var pattern in patterns)
                if (pattern != null && !entry.Patterns.Exists(p => p.Data == pattern))
                    entry.Patterns.Add(new PatternState { Data = pattern, Remaining = pattern.triggerType == PatternTriggerType.Phase ? 0f : Mathf.Max(MinimumInterval, pattern.interval) });
        foreach (var state in entry.Patterns)
        {
            if (state.Data is not PlacementBossPatternData placement) continue;
            if (entry.Placement == null)
            {
                entry.Placement = boss.GetComponent<PatternLabDriver>() ?? boss.gameObject.AddComponent<PatternLabDriver>();
                entry.Placement.Configure(boss, _gridManager, _gameManager, _fieldPause, placement.TelegraphMaterial, true);
            }
            if (placement.Phases == null || placement.Phases.Thresholds == null) continue;
            var group = entry.PhaseGroups.Find(g => g.Data == placement.Phases);
            if (group == null)
            {
                group = new PlacementPhaseState { Data = placement.Phases, Fired = new bool[placement.Phases.Thresholds.Length] };
                entry.PhaseGroups.Add(group);
            }
            group.Skills.Add(state);
        }
        entry.HpHandler = (current, maximum) =>
        {
            if (current <= 0) { CancelCast(entry); return; }
            foreach (var state in entry.Patterns)
            {
                if (state.Data is PlacementBossPatternData || state.PhaseFired || state.Data.triggerType == PatternTriggerType.Timer) continue;
                if (maximum > 0 && current / maximum <= (decimal)state.Data.hpThreshold)
                { state.PhaseFired = true; state.PhaseReady = true; state.Remaining = 0f; }
            }
            foreach (var group in entry.PhaseGroups)
                for (int i = 0; i < group.Fired.Length; i++)
                {
                    if (group.Fired[i] || maximum <= 0 || current / maximum > (decimal)group.Data.Thresholds[i]) continue;
                    group.Fired[i] = true;
                    var selected = group.Skills[UnityEngine.Random.Range(0, group.Skills.Count)];
                    selected.Remaining = 0f;
                    entry.PhaseQueue.Enqueue(selected);
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
        CancelCast(entry);
        _entries.Remove(boss);
    }

    /// <summary>웨이브 전환/전투 종료 시 패턴, 셀 효과와 스턴을 정리한다.</summary>
    public void UnregisterAll()
    {
        foreach (var pair in _entries)
        {
            if (pair.Key != null) pair.Key.OnHpChanged -= pair.Value.HpHandler;
            pair.Value.Runtime?.ClearPatternEffects();
            pair.Value.Placement?.ResetAll();
            pair.Value.SpineAnimator?.ReturnToIdle();
        }
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

    private void LateUpdate() => AdvanceCombat(Time.deltaTime);

    /// <summary>Advances the combat scheduler, respecting terminal state and selection holds. Isolated previews use the same path.</summary>
    public void AdvanceCombat(float deltaTime)
    {
        if (_gameManager != null && (_gameManager.CurrentState == GameManager.GameState.Win || _gameManager.CurrentState == GameManager.GameState.Lose))
        { if (_entries.Count > 0) UnregisterAll(); return; }
        if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)
            || (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing)) return;
        if (_fieldPause != null && _fieldPause.AttacksHeld) return; // 선택 화면 유예 중 — 패턴 진행 정지
        _iteration.Clear();
        _iteration.AddRange(_entries.Keys);
        foreach (var boss in _iteration)
        {
            if (boss == null || boss.IsDead) { UnregisterBoss(boss); continue; }
            if (!_entries.TryGetValue(boss, out var entry)) continue;
            try { Tick(boss, entry, deltaTime); }
            catch (Exception error) { CancelCast(entry); Debug.LogException(error, boss); }
        }
    }

    private void Tick(BossBase boss, BossPatternEntry entry, float deltaTime)
    {
        entry.Runtime?.AdvancePatternEffects(deltaTime);
        entry.Placement?.Advance(deltaTime);
        foreach (var state in entry.Patterns)
            if (state.Timed || state.Remaining > 0f) state.Remaining = Mathf.Max(0f, state.Remaining - deltaTime);
        if (entry.Casting != null)
        {
            entry.Elapsed += deltaTime;
            if ((entry.Casting.patternType == BossPatternType.Earthquake) && !entry.ImpactApplied && entry.Elapsed >= entry.ImpactTime)
            {
                // 판정 도래 프레임의 모든 피해/전투 종료 처리를 먼저 끝낸다.
                if (entry.PendingImpactFrame < 0) entry.PendingImpactFrame = Time.frameCount;
                else if (Time.frameCount > entry.PendingImpactFrame)
                {
                    entry.ImpactApplied = true;
                    ApplyEarthquake(entry);
                }
            }
            if (entry.Casting is PlacementBossPatternData placement)
            {
                if (entry.Placement != null && entry.Placement.IsBusy || entry.Elapsed < placement.NativeDuration) return;
            }
            else if (entry.Elapsed < entry.Duration || ((entry.Casting.patternType == BossPatternType.Earthquake) && !entry.ImpactApplied)) return;
            ResetCast(entry);
        }

        bool hasPhase = entry.PhaseQueue.Count > 0;
        PatternState next = entry.Requested ?? (hasPhase ? entry.PhaseQueue.Peek() : null);
        if (next != null && entry.Requested == null && next.Remaining > 0f) return;
        foreach (var state in entry.Patterns)
            if (state.Remaining <= 0f && (state.Timed || state.PhaseReady)
                && entry.Requested == null && !hasPhase && (next == null || state.Data.Priority > next.Data.Priority)) next = state;
        if (next == null) return;
        var data = next.Data;
        if (data is PlacementBossPatternData placementData &&
            !placementData.TryStartRandom(entry.Placement))
        { next.Remaining = Mathf.Max(MinimumInterval, data.RetryDelay); return; }
        if (data.patternType == BossPatternType.Earthquake && !HasTarget())
        { next.Remaining = Mathf.Max(MinimumInterval, data.RetryDelay); return; }
        next.Remaining = Mathf.Max(MinimumInterval, data.interval);
        if (entry.Requested == null && hasPhase) entry.PhaseQueue.Dequeue();
        entry.Requested = null;
        next.PhaseReady = false;
        entry.Casting = data;
        entry.Duration = Mathf.Max(data.CastDuration, data.patternType == BossPatternType.Earthquake ? data.ImpactTimeSec : 0f);
        entry.ImpactTime = Mathf.Max(0f, data.ImpactTimeSec);
        entry.StunDuration = Mathf.Max(0f, data.duration);
        entry.Visual = data.StunVisual;
        entry.UseShake = data.UseScreenShake;
        entry.ShakeDuration = data.ShakeDuration;
        entry.ShakeIntensity = data.ShakeIntensity;
        boss.PlayPatternAnimation(data);
        if (data.patternType == BossPatternType.Earthquake) _gridManager?.SetBossTelegraphAll(true);
        else if (data is not PlacementBossPatternData) boss.ExecutePattern(data);
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
    /// <summary>시전을 취소하고, 판정 전 예고 중이었으면 셀 텔레그래프도 끈다 (보스 사망/해제 시 잔상 방지).</summary>
    private void CancelCast(BossPatternEntry entry)
    {
        bool wasTelegraphing = IsCounterable(entry);
        entry.Runtime?.ClearPatternEffects();
        entry.Placement?.ResetAll();
        entry.SpineAnimator?.ReturnToIdle();
        ResetCast(entry);
        if (wasTelegraphing) _gridManager?.SetBossTelegraphAll(false);
    }
    private static void ResetCast(BossPatternEntry entry)
    {
        entry.Casting = null;
        entry.Visual = null;
        entry.Elapsed = 0f;
        entry.ImpactApplied = false;
        entry.PendingImpactFrame = -1;
    }
    private void OnDestroy()
    {
        ChiefActiveSkillSignals.OnActivated -= HandleChiefSkillActivated;
        UnregisterAll();
    }
}
