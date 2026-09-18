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
        _uiManager?.ConfigureBossDebuffs(this, _debuffSettings);
    }

    [Inject] private GameDataManager _gameDataManager;
    [Inject] private WaveManager _waveManager;
    [Inject] private UIManager _uiManager;
    [Inject] private GameManager _gameManager;
    [Inject] private DebuffCatalog _debuffCatalog;
    [Inject] private DebuffSettings _debuffSettings;
    [Inject] private BossPatternController _patternController;
    [Inject] private TimerController _combatTimer;

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

        if (entry?.Prefab == null)
        {
            Debug.LogError("[BossManager] BossEntry prefab 미연결");
            onDefeated?.Invoke();
            return;
        }

        var go   = Instantiate(entry.Prefab, bossSpawnPoint != null ? bossSpawnPoint.transform : transform);
        var boss = go.GetComponent<BossBase>();

        if (boss == null)
        {
            Debug.LogError($"[BossManager] [{entry.Prefab.name}]에 BossBase 없음");
            Destroy(go);
            onDefeated?.Invoke();
            return;
        }

        // BossData가 지정되면 해당 SO가 수치의 원본. 기존 웨이브만 시트 우선 경로를 유지한다.
        bool useSheet = entry.Data == null && _gameDataManager != null && _gameDataManager.IsLoaded && _waveManager != null;
        decimal hp = useSheet ? _gameDataManager.GetBossMaxHp(100 + _waveManager.CurrentWave, entry.MaxHp) : entry.MaxHp;
        double defense = useSheet ? _gameDataManager.GetBossDefense(100 + _waveManager.CurrentWave, entry.BaseDefense) : entry.BaseDefense;
        float expMultiplier = useSheet ? _gameDataManager.GetExpMultiplierForRound(100 + _waveManager.CurrentWave) : entry.ExpMultiplier;
        boss.ConfigureDebuffs(_debuffCatalog, _debuffSettings, _gameManager, defense, _combatTimer);
        boss.Init(hp);
        boss.ExpMultiplier = expMultiplier;

        _currentBosses.Add(boss);

        boss.OnHpChanged += (cur, max) =>
        {
            if (cur <= 0) _combatTimer?.StopTimer();
            _uiManager?.UpdateBossHp(cur, max);
        };
        boss.OnDeath     += () =>
        {
            _patternController?.UnregisterBoss(boss);
            _currentBosses.Remove(boss);
            Destroy(boss.gameObject);
            onDefeated?.Invoke();
        };

        boss.gameObject.AddComponent<BossAreaTarget>();

        _patternController?.RegisterBoss(boss, boss.Patterns);
        _uiManager?.BeginBossHp(boss.CurrentHp, boss.MaxHp, entry.HpLineCount);

        OnBossEntryed?.Invoke(_prevBossEntry, entry);
        _prevBossEntry = entry;
        Debug.Log($"[BossManager] 보스 소환: {entry.Prefab.name} (HP: {boss.MaxHp}, 줄 수: {entry.HpLineCount})");
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void ClearAllBosses()
    {
        _patternController?.UnregisterAll();

        foreach (var boss in _currentBosses)
        {
            if (boss == null) continue;
            boss.ClearListeners();
            Destroy(boss.gameObject);
        }

        _currentBosses.Clear();
    }
}
