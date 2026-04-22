using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 웨이브 흐름 관리
///
/// 순차 보스 소환 흐름:
///   SpawnWaveBosses()
///     └─ SpawnNextBoss()
///           보스 사망
///             ├─ 다음 보스 있음 → 토템 보상 → SpawnNextBoss()
///             └─ 모두 처치    → OnAllBossesDefeated()
/// </summary>
public class WaveManager : InGameSingleton<WaveManager>
{
    [SerializeField] private StageData stageData;

    public int CurrentWave { get; private set; } = 0;
    public int TotalWaves  => stageData != null ? stageData.waves.Length : 0;

    private List<BossEntry> _pendingBosses = new List<BossEntry>();
    private int             _bossIndex     = 0;

    // ── 외부 호출 ──────────────────────────────────────────────

    public void StartWave() => SpawnWaveBosses();

    // ── 웨이브 소환 ────────────────────────────────────────────

    private void SpawnWaveBosses()
    {
        if (stageData == null || stageData.waves == null || CurrentWave >= stageData.waves.Length)
        {
            Debug.LogError("[WaveManager] StageData 또는 waves 배열이 올바르지 않습니다.");
            return;
        }

        var wave = stageData.waves[CurrentWave];
        Manager.UI.UpdateWaveText(CurrentWave + 1, TotalWaves);

        // 유효한 보스만 추림
        _pendingBosses.Clear();
        foreach (var entry in wave.bosses)
            if (entry?.prefab != null) _pendingBosses.Add(entry);

        if (_pendingBosses.Count == 0)
        {
            Debug.LogWarning("[WaveManager] 유효한 보스 없음 — 웨이브 즉시 완료");
            OnAllBossesDefeated();
            return;
        }

        _bossIndex = 0;
        SpawnNextBoss();
    }

    /// <summary>_bossIndex번째 보스 소환</summary>
    private void SpawnNextBoss()
    {
        var entry = _pendingBosses[_bossIndex];

        Manager.Boss.SpawnSingleBoss(entry, OnSingleBossDefeated);

        // 유닛 타겟 갱신
        var mainBoss = Manager.Boss.CurrentBoss;
        foreach (var cell in Manager.Grid.GetOccupiedCells())
        {
            if (cell.OccupyingUnit == null) continue;
            cell.OccupyingUnit.OnRemoved();
            cell.OccupyingUnit.OnPlaced(Manager.Currency, mainBoss);
        }
    }

    /// <summary>보스 1마리 처치 시 호출</summary>
    private void OnSingleBossDefeated()
    {
        // 랜덤 토템 즉시 지급
        GiveRandomTotem();

        _bossIndex++;

        if (_bossIndex < _pendingBosses.Count)
            SpawnNextBoss();
        else
            OnAllBossesDefeated();
    }

    /// <summary>웨이브의 모든 보스 처치 완료</summary>
    private void OnAllBossesDefeated()
    {
        CurrentWave++;

        if (CurrentWave >= TotalWaves)
        {
            Manager.Game.OnAllWavesCleared();
            return;
        }

        SpawnWaveBosses();
    }

    private void GiveRandomTotem()
    {
        bool placed = Manager.Totem.SpawnRandomTotem();
        if (!placed)
            Debug.Log("[WaveManager] 빈 셀 없음 — 토템 지급 스킵");
    }
}
