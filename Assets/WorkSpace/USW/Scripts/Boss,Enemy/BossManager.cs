using UnityEngine;
using VContainer;
using System;
using System.Collections.Generic;

/// <summary>
/// 보스 단일 소환 및 현재 보스 참조 관리
///
/// 순차 소환 흐름은 WaveManager가 담당.
/// BossManager는 보스 1마리씩 소환/해제만 처리.
/// </summary>
public class BossManager : MonoBehaviour
{
 
    public void Init()
    {

        
    }

    [Inject] private GameDataManager _gameDataManager;
    [Inject] private WaveManager _waveManager;
    [Inject] private UIManager _uiManager;

    [SerializeField] private GameObject bossSpawnPoint;
    [Tooltip("보스 표시 크기 (px) — 1080×2340 기준 300 권장")]
    [SerializeField] private Vector2 bossSize = new Vector2(300f, 300f);

    private readonly List<BossBase> _currentBosses = new List<BossBase>();
    private BossEntry _prevBossEntry;

    public IReadOnlyList<BossBase> CurrentBosses => _currentBosses;
    public BossBase CurrentBoss => _currentBosses.Count > 0 ? _currentBosses[0] : null;
    public BossEntry PrevBossEntry => _prevBossEntry;
    public Action<BossEntry, BossEntry> OnBossEntryed; // Changed to pass both prev and current

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

        var go   = Instantiate(entry.prefab, bossSpawnPoint != null ? bossSpawnPoint.transform : transform);
        var boss = go.GetComponent<BossBase>();

        if (boss == null)
        {
            Debug.LogError($"[BossManager] [{entry.prefab.name}]에 BossBase 없음");
            Destroy(go);
            onDefeated?.Invoke();
            return;
        }

        // 시트 HP 우선 — 미로드 시 WaveData SO의 hp 폴백
        int hp = _gameDataManager != null && _gameDataManager.IsLoaded
            ? _gameDataManager.GetBossMaxHp(100 + _waveManager.CurrentWave, entry.hp)
            : entry.hp;
        boss.Init(hp);

        _currentBosses.Add(boss);

        boss.OnHpChanged += (cur, max) => _uiManager.UpdateBossHp(cur, max);
        boss.OnDeath     += () =>
        {
            BossPatternController.Instance.UnregisterBoss(boss);
            _currentBosses.Remove(boss);
            Destroy(boss.gameObject);
            onDefeated?.Invoke();
        };

        boss.gameObject.AddComponent<BossAreaTarget>();

        BossPatternController.Instance.RegisterBoss(boss, boss.Patterns);
        _uiManager.UpdateBossHp(boss.CurrentHp, boss.MaxHp);

        OnBossEntryed?.Invoke(_prevBossEntry, entry);
        _prevBossEntry = entry;
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
}
