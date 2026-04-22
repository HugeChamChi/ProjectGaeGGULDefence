using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 보스 단일 소환 및 현재 보스 참조 관리
///
/// 순차 소환 흐름은 WaveManager가 담당.
/// BossManager는 보스 1마리씩 소환/해제만 처리.
/// </summary>
public class BossManager : InGameSingleton<BossManager>
{
    [SerializeField] private RectTransform bossSpawnPoint;
    [Tooltip("보스 표시 크기 (px) — 1080×2340 기준 300 권장")]
    [SerializeField] private Vector2 bossSize = new Vector2(300f, 300f);

    private readonly List<BossBase> _currentBosses = new List<BossBase>();

    public IReadOnlyList<BossBase> CurrentBosses => _currentBosses;
    public BossBase CurrentBoss => _currentBosses.Count > 0 ? _currentBosses[0] : null;

    // ── 외부 API ──────────────────────────────────────────────────

    /// <summary>
    /// 보스 1마리 소환. 죽으면 onDefeated 콜백.
    /// WaveManager.SpawnNextBoss()에서 호출.
    /// </summary>
    public void SpawnSingleBoss(BossEntry entry, Action onDefeated)
    {
        ClearAllBosses();

        if (entry?.prefab == null)
        {
            Debug.LogError("[BossManager] BossEntry prefab 미연결");
            onDefeated?.Invoke();
            return;
        }

        var go   = Instantiate(entry.prefab, bossSpawnPoint);
        var boss = go.GetComponent<BossBase>();

        if (boss == null)
        {
            Debug.LogError($"[BossManager] [{entry.prefab.name}]에 BossBase 없음");
            Destroy(go);
            onDefeated?.Invoke();
            return;
        }

        SetupRectTransform(go);
        boss.Init(entry.hp);

        _currentBosses.Add(boss);

        boss.OnHpChanged += (cur, max) => Manager.UI.UpdateBossHp(cur, max);
        boss.OnDamaged   += amount => Manager.Exp.AddExp(amount);
        boss.OnDeath     += () =>
        {
            BossPatternController.Instance.UnregisterBoss(boss);
            _currentBosses.Remove(boss);
            onDefeated?.Invoke();
        };

        boss.gameObject.AddComponent<BossAreaTarget>();

        BossPatternController.Instance.RegisterBoss(boss, boss.Patterns);
        Manager.UI.UpdateBossHp(boss.CurrentHp, boss.MaxHp);

        Debug.Log($"[BossManager] 보스 소환: {entry.prefab.name} (HP: {entry.hp})");
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void ClearAllBosses()
    {
        BossPatternController.Instance.UnregisterAll();

        foreach (var boss in _currentBosses)
        {
            if (boss == null) continue;
            boss.ClearListeners();
            Destroy(boss.gameObject);
        }

        _currentBosses.Clear();
    }

    private void SetupRectTransform(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = bossSize;
    }
}
