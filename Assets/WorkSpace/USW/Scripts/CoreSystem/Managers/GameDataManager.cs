using System;
using VContainer;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 잔존 시트 2개를 병렬 로드합니다. 캐릭터·게임 설정·토템은 에디터에서 관리하는 SO를 사용합니다.
/// _gameDataManager 로 접근합니다.
///
/// 담당 시트:
///   - 소환 등장 확률    (gid=1984586417)
///   - 보스 HP/경험치    (gid=1622284954)
/// </summary>
public class GameDataManager
{
    private readonly DebuffCatalog _debuffCatalog;
    private readonly DebuffSettings _debuffSettings;
    /// <summary>루트 수명 정의 저장소와 로딩 설정 주입.</summary>
    public GameDataManager(DebuffCatalog debuffCatalog, DebuffSettings debuffSettings)
    { _debuffCatalog = debuffCatalog; _debuffSettings = debuffSettings; }
 
    public void Init()
    {
    }

    private const string BaseUrl = "https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/export?format=csv&gid=";

    private const string GidSpawnRate = "1984586417";
    private const string GidBoss = "1622284954";

    public bool IsLoaded { get; private set; }
    public event Action OnLoaded;

    // ── 소환 비용 ─────────────────────────────────────────
    public int SummonInitialCost { get; private set; } = 20;
    public int SummonCostIncrease { get; private set; } = 20;

    // ── 소환 등장 확률 ────────────────────────────────────
    private readonly Dictionary<int, float> _spawnRates = new();
    private float _totalSpawnWeight;

    // ── 보스 데이터 ───────────────────────────────────────
    private readonly Dictionary<int, BossSheetRow> _bossByRoundId = new();
    private int _currentBossRoundId = -1;

    public async UniTask LoadAllAsync(CancellationToken token = default)
    {
        IsLoaded = false;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var (spawnCsv, bossCsv) = await UniTask.WhenAll(
                    FetchCsvAsync(GidSpawnRate, token),
                    FetchCsvAsync(GidBoss, token)
                );

                if (spawnCsv == null || bossCsv == null)
                {
                    Debug.LogWarning("[GameDataManager] 시트 다운로드 일부 실패. 3초 후 전체 재시도합니다...");
                    await UniTask.Delay(3000, DelayType.Realtime, cancellationToken: token);
                    continue;
                }

                ParseSpawnRates(spawnCsv);
                List<DebuffDefinition> pendingDebuffs = null;
                if (!string.IsNullOrWhiteSpace(_debuffSettings.SheetGid))
                {
                    string debuffCsv = await FetchCsvAsync(_debuffSettings.SheetGid, token);
                    if (string.IsNullOrWhiteSpace(debuffCsv)) throw new FormatException("Debuff CSV could not be loaded.");
                    pendingDebuffs = DebuffSheetParser.Parse(SplitCsvRows(debuffCsv));
                }
                ParseBossData(bossCsv);
                ValidateDebuffBindings(pendingDebuffs);
                if (pendingDebuffs != null) _debuffCatalog.Replace(pendingDebuffs);

                IsLoaded = true;
                OnLoaded?.Invoke();
                Debug.Log("[GameDataManager] 모든 시트 로드 및 파싱 완료");
                break; // 성공 시 무한루프 탈출
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Debug.LogError($"[GameDataManager] 파싱 중 에러 발생: {ex.Message}\n3초 후 전체 재시도합니다...");
                await UniTask.Delay(3000, DelayType.Realtime, cancellationToken: token);
            }
        }
    }

    private void ValidateDebuffBindings(List<DebuffDefinition> pending)
    {
        var definitions = pending != null ? DebuffCatalog.Validate(pending) : null;
        if (definitions != null)
            foreach (var local in _debuffSettings.CreateDefinitions())
                if (!definitions.TryGetValue(local.Id, out var remote) || remote.Kind != local.Kind || remote.StackGroup != local.StackGroup)
                    throw new FormatException($"Debuff sheet must preserve local identity {local.Id}/{local.Key}/{local.StackGroup}.");
    }

    private async UniTask<string> FetchCsvAsync(string gid, CancellationToken token)
    {
        if (string.IsNullOrEmpty(gid) || gid == "0") return ""; // GID가 미설정된 경우 무시

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

    // ── 기존 파싱 ─────────────────────────────────────────

    private void ParseSpawnRates(string csv)
    {
        var lines = csv.Split('\n');
        _totalSpawnWeight = 0f;
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 4 || !int.TryParse(cols[0].Trim(), out var id)) continue;
            if (!float.TryParse(cols[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rate)) continue;

            _spawnRates[id] = rate;
            _totalSpawnWeight += rate;
        }
    }


    private void ParseBossData(string csv)
    {
        _bossByRoundId.Clear();
        var lines = csv.Split('\n');
        var bossHeaders = ParseCsvRow(lines[0]);
        int defenseColumn = DebuffSheetParser.Column(bossHeaders, "defense");
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 6) continue;
            if (!int.TryParse(cols[0].Trim(), out var bossId)) continue;
            if (!int.TryParse(cols[1].Trim(), out var roundId)) continue;
            if (!decimal.TryParse(cols[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var hp))
                throw new FormatException($"Boss row {i + 1}: invalid HP.");
            if (CombatHealth.ToUnits(hp) <= 0) throw new FormatException($"Boss row {i + 1}: HP must be positive.");
            double? defense = null;
            if (defenseColumn >= 0 && defenseColumn < cols.Length && !string.IsNullOrWhiteSpace(cols[defenseColumn]))
            {
                defense = double.Parse(cols[defenseColumn], NumberStyles.Float, CultureInfo.InvariantCulture);
                if (defense < 0 || double.IsNaN(defense.Value) || double.IsInfinity(defense.Value))
                    throw new FormatException($"Boss row {i + 1}: invalid defense.");
            }
            if (!float.TryParse(cols[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var expPerHp)) continue;
            if (!float.TryParse(cols[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var expAmt)) continue;

            _bossByRoundId[roundId] = new BossSheetRow
            {
                BossId = bossId,
                RoundId = roundId,
                MaxHealth = hp,
                Defense = defense,
                DropExpPerHealth = expPerHp,
                DropExpAmount = expAmt,
            };
        }
    }

    /// <summary>
    /// 쌍따옴표 내의 줄바꿈을 지원하는 전체 CSV 파서.
    /// </summary>
    public static List<string[]> SplitCsvRows(string csv)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                
                fields.Add(current.ToString().Trim());
                rows.Add(fields.ToArray());
                fields.Clear();
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        if (fields.Count > 0 || current.Length > 0)
        {
            fields.Add(current.ToString().Trim());
            rows.Add(fields.ToArray());
        }

        return rows;
    }

    /// <summary>
    /// 따옴표로 묶인 필드(콤마 포함 가능)를 올바르게 파싱하는 CSV 행 분리기.
    /// "value","with, comma","escaped ""quote""" 형식 지원.
    /// </summary>
    public static string[] ParseCsvRow(string line)
    {
        line = line.TrimEnd('\r');
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString().Trim());
        return fields.ToArray();
    }

    // ── 외부 API ─────────────────────────────────────────

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

    public int GetSellPrice(int characterId, int level)
    {
        return 0; // 시트 삭제됨
    }

    public decimal GetBossMaxHp(int roundId, decimal fallback = 1000)
        => _bossByRoundId.TryGetValue(roundId, out var row) ? row.MaxHealth : fallback;

    /// <summary>시트에서 지정한 방어력 또는 보스 SO 설정.</summary>
    public double GetBossDefense(int roundId, double fallback)
        => _bossByRoundId.TryGetValue(roundId, out var row) ? row.Defense ?? fallback : fallback;

    public float GetExpMultiplierForRound(int roundId)
    {
        if (!_bossByRoundId.TryGetValue(roundId, out var row) || row.MaxHealth <= 0) return 0.01f;
        
        // (DropExpAmount / DropExpPerHealth)는 보스를 100% 잡았을 때 줄 총 경험치 양입니다.
        // 이를 MaxHealth로 나누어 데미지 1당 줄 경험치 수치를 계산합니다.
        float totalExpForBoss = row.DropExpAmount / Mathf.Max(row.DropExpPerHealth, 0.001f);
        return totalExpForBoss / (float)row.MaxHealth;
    }

    public void SetCurrentBossRound(int roundId) => _currentBossRoundId = roundId;

    public float GetCurrentExpMultiplier()
        => _currentBossRoundId >= 0 ? GetExpMultiplierForRound(_currentBossRoundId) : 0.01f;

    // ── 내부 데이터 클래스 ────────────────────────────────

    private class BossSheetRow
    {
        public int BossId;
        public int RoundId;
        public decimal MaxHealth;
        public double? Defense;
        public float DropExpPerHealth;
        public float DropExpAmount;
    }

}
