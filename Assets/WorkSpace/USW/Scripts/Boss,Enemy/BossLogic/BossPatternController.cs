using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 보스 패턴 발동 타이밍 관리
///
/// 변경 사항:
///   - 단일 보스 → 다중 보스 지원
///     Dictionary 기반으로 보스별 패턴/코루틴/상태를 독립 관리
///   - RegisterBoss(boss, patterns) : 보스별 등록
///   - UnregisterBoss(boss)         : 개별 해제
///   - UnregisterAll()              : 웨이브 교체 등 전체 해제
/// </summary>
public class BossPatternController : InGameSingleton<BossPatternController>
{
    private class BossPatternEntry
    {
        public BossPatternData[]        patterns;
        public HashSet<BossPatternData> firedPhasePatterns = new HashSet<BossPatternData>();
        public List<Coroutine>          timerCoroutines    = new List<Coroutine>();
        public Action<int, int>         hpHandler;
    }

    private readonly Dictionary<BossBase, BossPatternEntry> _entries = new Dictionary<BossBase, BossPatternEntry>();

    // ── 외부 API ──────────────────────────────────────────────────

    /// <summary>BossManager.SpawnBosses() 내부에서 보스별로 호출</summary>
    public void RegisterBoss(BossBase boss, BossPatternData[] patterns)
    {
        if (boss == null) return;

        UnregisterBoss(boss);

        var entry = new BossPatternEntry { patterns = patterns };

        if (patterns == null || patterns.Length == 0)
        {
            _entries[boss] = entry;
            return;
        }

        entry.hpHandler = (cur, max) => CheckPhasePatterns(boss, entry, cur, max);
        boss.OnHpChanged += entry.hpHandler;

        foreach (var pattern in patterns)
        {
            if (pattern == null) continue;

            if (pattern.triggerType == PatternTriggerType.Timer ||
                pattern.triggerType == PatternTriggerType.Both)
            {
                var co = StartCoroutine(TimerPatternRoutine(boss, pattern));
                entry.timerCoroutines.Add(co);
            }
        }

        _entries[boss] = entry;
        Debug.Log($"[BossPatternController] {boss.name} 패턴 {patterns.Length}개 등록");
    }

    /// <summary>보스 사망 또는 개별 교체 시 호출</summary>
    public void UnregisterBoss(BossBase boss)
    {
        if (boss == null || !_entries.TryGetValue(boss, out var entry)) return;

        if (entry.hpHandler != null)
            boss.OnHpChanged -= entry.hpHandler;

        foreach (var co in entry.timerCoroutines)
            if (co != null) StopCoroutine(co);

        _entries.Remove(boss);
    }

    /// <summary>웨이브 전체 교체 시 모든 보스 정리</summary>
    public void UnregisterAll()
    {
        foreach (var kvp in _entries)
        {
            var boss  = kvp.Key;
            var entry = kvp.Value;

            if (boss != null && entry.hpHandler != null)
                boss.OnHpChanged -= entry.hpHandler;

            foreach (var co in entry.timerCoroutines)
                if (co != null) StopCoroutine(co);
        }

        _entries.Clear();

        foreach (var cell in Manager.Grid.AllCells())
            cell.Model.ClearBossDebuffs();
    }

    // ── 타이머 기반 패턴 ──────────────────────────────────────────

    private IEnumerator TimerPatternRoutine(BossBase boss, BossPatternData pattern)
    {
        yield return new WaitForSeconds(pattern.interval);

        while (_entries.ContainsKey(boss) && !boss.IsDead)
        {
            boss.ExecutePattern(pattern);
            yield return new WaitForSeconds(pattern.interval);
        }
    }

    // ── 페이즈 기반 패턴 ──────────────────────────────────────────

    private void CheckPhasePatterns(BossBase boss, BossPatternEntry entry, int currentHp, int maxHp)
    {
        if (entry.patterns == null) return;

        float hpRatio = maxHp > 0 ? (float)currentHp / maxHp : 0f;

        foreach (var pattern in entry.patterns)
        {
            if (pattern == null) continue;
            if (pattern.triggerType != PatternTriggerType.Phase &&
                pattern.triggerType != PatternTriggerType.Both) continue;
            if (entry.firedPhasePatterns.Contains(pattern)) continue;

            if (hpRatio <= pattern.hpThreshold)
            {
                entry.firedPhasePatterns.Add(pattern);
                boss.ExecutePattern(pattern);
                Debug.Log($"[BossPatternController] {boss.name} 페이즈 패턴 발동: {pattern.patternName} " +
                          $"(HP {hpRatio * 100f:F0}%)");
            }
        }
    }
}
