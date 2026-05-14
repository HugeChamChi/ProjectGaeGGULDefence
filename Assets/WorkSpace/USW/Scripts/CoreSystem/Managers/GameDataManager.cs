using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 시트 6개를 병렬 fetch하여 인게임 핵심 수치를 런타임에 제공합니다.
/// Manager.GameData 로 접근합니다.
///
/// 담당 시트:
///   - 소환 비용        (gid=1607115777)
///   - 소환 등장 확률    (gid=1984586417)
///   - 판매 가격        (gid=284585537)
///   - 재화 생산량       (gid=1326045266)
///   - 보스 HP/경험치    (gid=1622284954)
///   - 레벨업 경험치     (gid=499762901)
/// </summary>
public class GameDataManager : InGameSingleton<GameDataManager>
{
    private const string BaseUrl = "https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/export?format=csv&gid=";

    private const string GidSummonCost   = "1607115777";
    private const string GidSpawnRate    = "1984586417";
    private const string GidSellPrice    = "284585537";
    private const string GidCurrency     = "1326045266";
    private const string GidBoss         = "1622284954";
    private const string GidExpTable     = "499762901";

    public bool   IsLoaded { get; private set; }
    public event Action OnLoaded;

    // ── 소환 비용 ─────────────────────────────────────────
    public int SummonInitialCost   { get; private set; } = 20;
    public int SummonCostIncrease  { get; private set; } = 20;

    // ── 소환 등장 확률 ────────────────────────────────────
    // characterId → spawn_rate (가중치)
    private readonly Dictionary<int, float> _spawnRates = new();
    private float _totalSpawnWeight;

    // ── 판매 가격 ─────────────────────────────────────────
    // (characterId, upgradeLevel) → sell_price
    private readonly Dictionary<(int, int), int> _sellPrices = new();

    // ── 재화 생산량 ───────────────────────────────────────
    // characterId → currency_per_second
    private readonly Dictionary<int, float> _currencyPerSecond = new();

    // ── 보스 데이터 ───────────────────────────────────────
    // round_id → BossSheetRow
    private readonly Dictionary<int, BossSheetRow> _bossByRoundId = new();
    private int _currentBossRoundId = -1;

    // ── 레벨업 경험치 ─────────────────────────────────────
    // index i = level (i+1) 필요 exp. 길이 19 (Lv1→2 ~ Lv19→20)
    private float[] _expTable;

    // ── 초기화 ────────────────────────────────────────────

    private async UniTaskVoid Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        await LoadAllAsync(token);
    }

    private async UniTask LoadAllAsync(CancellationToken token)
    {
        var (csv0, csv1, csv2, csv3, csv4, csv5) = await UniTask.WhenAll(
            FetchCsvAsync(GidSummonCost,  token),
            FetchCsvAsync(GidSpawnRate,   token),
            FetchCsvAsync(GidSellPrice,   token),
            FetchCsvAsync(GidCurrency,    token),
            FetchCsvAsync(GidBoss,        token),
            FetchCsvAsync(GidExpTable,    token)
        );

        if (csv0 != null) ParseSummonCost(csv0);
        if (csv1 != null) ParseSpawnRates(csv1);
        if (csv2 != null) ParseSellPrices(csv2);
        if (csv3 != null) ParseCurrencyPerSecond(csv3);
        if (csv4 != null) ParseBossData(csv4);
        if (csv5 != null) ParseExpTable(csv5);

        IsLoaded = true;
        OnLoaded?.Invoke();
        Debug.Log("[GameDataManager] 모든 시트 로드 완료");
    }

    private async UniTask<string> FetchCsvAsync(string gid, CancellationToken token)
    {
        var req = UnityWebRequest.Get(BaseUrl + gid);
        try
        {
            await req.SendWebRequest().WithCancellation(token);
            if (req.result == UnityWebRequest.Result.Success)
                return req.downloadHandler.text;

            Debug.LogWarning($"[GameDataManager] 시트 로드 실패 gid={gid}: {req.error}");
            return null;
        }
        finally
        {
            req.Dispose();
        }
    }

    // ── 파싱 ─────────────────────────────────────────────

    // 헤더 3줄(컬럼명 + 타입 + 주석) 스킵. 데이터 1행
    private void ParseSummonCost(string csv)
    {
        var lines = csv.Split('\n');
        for (int i = 3; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 2 || string.IsNullOrWhiteSpace(cols[0])) continue;
            if (int.TryParse(cols[0].Trim(), out var ic)) SummonInitialCost  = ic;
            if (int.TryParse(cols[1].Trim(), out var ci)) SummonCostIncrease = ci;
            break;
        }
    }

    // 헤더 1줄 스킵
    private void ParseSpawnRates(string csv)
    {
        var lines = csv.Split('\n');
        _totalSpawnWeight = 0f;
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 4 || !int.TryParse(cols[0].Trim(), out var id)) continue;
            if (!float.TryParse(cols[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)) continue;

            _spawnRates[id]    = rate;
            _totalSpawnWeight += rate;
        }
    }

    // 헤더 1줄 스킵. character_id 없는 행(BM 전용) 스킵
    private void ParseSellPrices(string csv)
    {
        var lines = csv.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 5) continue;
            if (!int.TryParse(cols[0].Trim(), out var charId)) continue;
            if (!int.TryParse(cols[3].Trim(), out var level)) continue;
            if (!int.TryParse(cols[4].Trim(), out var price)) continue;

            _sellPrices[(charId, level)] = price;
        }
    }

    // 헤더 1줄 스킵
    private void ParseCurrencyPerSecond(string csv)
    {
        var lines = csv.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 4 || !int.TryParse(cols[0].Trim(), out var id)) continue;
            if (!float.TryParse(cols[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cps)) continue;

            _currencyPerSecond[id] = cps;
        }
    }

    // 헤더 1줄 스킵
    private void ParseBossData(string csv)
    {
        var lines = csv.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 6) continue;
            if (!int.TryParse(cols[0].Trim(), out var bossId)) continue;
            if (!int.TryParse(cols[1].Trim(), out var roundId)) continue;
            if (!int.TryParse(cols[3].Trim(), out var hp)) continue;
            if (!float.TryParse(cols[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var expPerHp)) continue;
            if (!float.TryParse(cols[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var expAmt)) continue;

            _bossByRoundId[roundId] = new BossSheetRow
            {
                BossId           = bossId,
                RoundId          = roundId,
                MaxHealth        = hp,
                DropExpPerHealth = expPerHp,
                DropExpAmount    = expAmt,
            };
        }
    }

    // 헤더 2줄(컬럼명 + 타입) 스킵
    private void ParseExpTable(string csv)
    {
        var table = new List<float>();
        var lines = csv.Split('\n');
        for (int i = 2; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 3 || string.IsNullOrWhiteSpace(cols[0])) continue;
            if (!float.TryParse(cols[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var exp)) continue;
            table.Add(exp);
        }
        _expTable = table.ToArray();
    }

    // ── 외부 API ─────────────────────────────────────────

    /// <summary>가중치 랜덤으로 소환될 characterId 반환. 실패 시 -1.</summary>
    public int GetRandomSpawnCharacterId()
    {
        if (_spawnRates.Count == 0) return -1;

        float roll = UnityEngine.Random.Range(0f, _totalSpawnWeight);
        float cumulative = 0f;
        foreach (var kv in _spawnRates)
        {
            cumulative += kv.Value;
            if (roll <= cumulative) return kv.Key;
        }
        return -1;
    }

    /// <summary>characterId + 현재 강화 레벨 기준 판매 가격. 데이터 없으면 0.</summary>
    public int GetSellPrice(int characterId, int upgradeLevel)
    {
        int clampedLevel = Mathf.Clamp(upgradeLevel, 1, 10);
        return _sellPrices.TryGetValue((characterId, clampedLevel), out var price) ? price : 0;
    }

    /// <summary>characterId 기준 초당 재화 생산량. 데이터 없으면 0.</summary>
    public float GetCurrencyPerSecond(int characterId)
        => _currencyPerSecond.TryGetValue(characterId, out var cps) ? cps : 0f;

    /// <summary>round_id 기준 보스 최대 체력. 데이터 없으면 fallback 반환.</summary>
    public int GetBossMaxHp(int roundId, int fallback = 1000)
        => _bossByRoundId.TryGetValue(roundId, out var row) ? row.MaxHealth : fallback;

    /// <summary>round_id 기준 데미지당 경험치 배율 (damage × 이 값 = exp 획득량).</summary>
    public float GetExpMultiplierForRound(int roundId)
    {
        if (!_bossByRoundId.TryGetValue(roundId, out var row)) return 0.01f;
        return row.DropExpAmount / row.DropExpPerHealth;
    }

    /// <summary>현재 보스 라운드 설정. WaveManager가 보스 spawn 시 호출.</summary>
    public void SetCurrentBossRound(int roundId) => _currentBossRoundId = roundId;

    /// <summary>현재 보스 기준 데미지당 경험치 배율.</summary>
    public float GetCurrentExpMultiplier()
        => _currentBossRoundId >= 0 ? GetExpMultiplierForRound(_currentBossRoundId) : 0.01f;

    /// <summary>레벨(1~20) 기준 다음 레벨까지 필요 경험치. 데이터 없으면 fallback.</summary>
    public float GetExpRequired(int level)
    {
        if (_expTable == null || _expTable.Length == 0) return 100f;
        int idx = Mathf.Clamp(level - 1, 0, _expTable.Length - 1);
        return _expTable[idx];
    }

    // ── 내부 데이터 클래스 ────────────────────────────────

    private class BossSheetRow
    {
        public int   BossId;
        public int   RoundId;
        public int   MaxHealth;
        public float DropExpPerHealth;
        public float DropExpAmount;
    }
}
