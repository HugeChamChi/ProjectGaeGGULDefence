using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 시트에서 강화 데이터를 로드하고, 강화 상태·비용·스탯 조회를 제공합니다.
/// Manager.Upgrade 로 접근합니다.
/// </summary>
public class UpgradeManager : InGameSingleton<UpgradeManager>
{
    private const string CostSheetUrl = "https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/export?format=csv&gid=297223937";
    private const string StatSheetUrl = "https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/export?format=csv&gid=1454519483";

    private const int MaxUpgradeLevel = 10;

    public bool   IsLoaded { get; private set; }
    public event Action OnLoaded;

    private readonly List<JobUpgradeCostRow>          _costRows        = new();
    private readonly Dictionary<int, CharacterStatRow[]> _statCache    = new();  // characterId → rows[level-1]
    private readonly Dictionary<int, string>          _charIdToJobType = new();

    private readonly Dictionary<string, int> _jobLevel = new()
    {
        { "Frog", 1 }, { "Frog_Gunner", 1 }, { "Frog_Ninja", 1 }, { "Frog_Magician", 1 }
    };
    private int _currencyLevel = 1;

    private async UniTaskVoid Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        await LoadAllAsync(token);
    }

    private async UniTask LoadAllAsync(CancellationToken token)
    {
        var (costCsv, statCsv) = await UniTask.WhenAll(
            FetchCsvAsync(CostSheetUrl, token),
            FetchCsvAsync(StatSheetUrl, token)
        );

        if (costCsv != null) ParseCostSheet(costCsv);
        if (statCsv  != null) ParseStatSheet(statCsv);

        IsLoaded = true;
        OnLoaded?.Invoke();
    }

    private async UniTask<string> FetchCsvAsync(string url, CancellationToken token)
    {
        var req = UnityWebRequest.Get(url);
        try
        {
            await req.SendWebRequest().WithCancellation(token);
            if (req.result == UnityWebRequest.Result.Success)
                return req.downloadHandler.text;

            Debug.LogWarning($"[UpgradeManager] 시트 로드 실패: {req.error}");
            return null;
        }
        finally
        {
            req.Dispose();
        }
    }

    // 헤더 2줄(컬럼명 + 타입) 스킵
    private void ParseCostSheet(string csv)
    {
        var lines = csv.Split('\n');
        for (int i = 2; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 5 || string.IsNullOrWhiteSpace(cols[0])) continue;

            _costRows.Add(new JobUpgradeCostRow
            {
                UpgradeType   = cols[0].Trim(),
                UpgradeTarget = cols[1].Trim(),
                InitialCost   = int.TryParse(cols[2].Trim(), out var ic) ? ic : 0,
                CostRate      = float.TryParse(cols[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cr) ? cr : 1f,
                CostIncrease  = int.TryParse(cols[4].Trim(), out var ci) ? ci : 0,
            });
        }
    }

    // 헤더 1줄(컬럼명) 스킵
    private void ParseStatSheet(string csv)
    {
        var lines = csv.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Trim().Split(',');
            if (cols.Length < 8 || string.IsNullOrWhiteSpace(cols[0])) continue;
            if (!int.TryParse(cols[0].Trim(), out var charId)) continue;

            int    level    = int.TryParse(cols[5].Trim(), out var lv) ? lv : 1;
            string charType = cols[3].Trim();

            var row = new CharacterStatRow
            {
                CharacterId   = charId,
                CharacterType = charType,
                Level         = level,
                Atk           = float.TryParse(cols[6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var atk) ? atk : 0f,
                AttackSpeed   = float.TryParse(cols[7].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var spd) ? spd : 1f,
            };

            if (!_statCache.ContainsKey(charId))
                _statCache[charId] = new CharacterStatRow[MaxUpgradeLevel];

            if (level >= 1 && level <= MaxUpgradeLevel)
            {
                _statCache[charId][level - 1] = row;
                if (!_charIdToJobType.ContainsKey(charId))
                    _charIdToJobType[charId] = charType;
            }
        }
    }

    /// <summary>characterId에 해당하는 직업 타입 문자열 반환. 데이터 없으면 빈 문자열.</summary>
    public string GetJobType(int characterId)
        => _charIdToJobType.TryGetValue(characterId, out var jt) ? jt : string.Empty;

    /// <summary>직업 타입의 현재 강화 레벨 반환. 없으면 1.</summary>
    public int GetJobLevel(string jobType)
        => _jobLevel.TryGetValue(jobType, out var lv) ? lv : 1;

    /// <summary>현재 강화 레벨 기준 공격력을 반환합니다.</summary>
    public float GetCurrentAtk(int characterId)
    {
        return TryGetCurrentStat(characterId, out var row) ? row.Atk : 0f;
    }

    /// <summary>현재 강화 레벨 기준 공격 간격(초)을 반환합니다. 낮을수록 빠름.</summary>
    public float GetCurrentAttackSpeed(int characterId)
    {
        return TryGetCurrentStat(characterId, out var row) ? row.AttackSpeed : 1f;
    }

    private bool TryGetCurrentStat(int characterId, out CharacterStatRow row)
    {
        row = null;
        if (!IsLoaded) return false;
        if (!_statCache.TryGetValue(characterId, out var stats)) return false;
        if (!_charIdToJobType.TryGetValue(characterId, out var jobType)) return false;

        int level = _jobLevel.TryGetValue(jobType, out var lv) ? lv : 1;
        row = stats[Mathf.Clamp(level, 1, MaxUpgradeLevel) - 1];
        return row != null;
    }

    /// <summary>다음 강화 비용을 반환합니다. 최대 레벨이거나 데이터 없으면 -1.</summary>
    public int GetUpgradeCost(string upgradeTarget)
    {
        var costRow = _costRows.Find(r => r.UpgradeTarget == upgradeTarget);
        if (costRow == null) return -1;

        int currentLevel = upgradeTarget == "All"
            ? _currencyLevel
            : (_jobLevel.TryGetValue(upgradeTarget, out var lv) ? lv : 1);

        if (currentLevel >= MaxUpgradeLevel) return -1;

        return CalculateCost(costRow, currentLevel);
    }

    private static int CalculateCost(JobUpgradeCostRow row, int currentLevel)
    {
        return row.UpgradeType == "Currency"
            ? row.InitialCost + row.CostIncrease * (currentLevel - 1)
            : Mathf.RoundToInt(row.InitialCost * Mathf.Pow(row.CostRate, currentLevel - 1));
    }

    /// <summary>강화를 시도합니다. 재화 부족 또는 최대 레벨이면 false.</summary>
    public bool TryUpgrade(string upgradeTarget)
    {
        int cost = GetUpgradeCost(upgradeTarget);
        if (cost < 0) return false;
        if (!Manager.Currency.Spend(cost)) return false;

        if (upgradeTarget == "All")
            _currencyLevel = Mathf.Min(_currencyLevel + 1, MaxUpgradeLevel);
        else if (_jobLevel.ContainsKey(upgradeTarget))
            _jobLevel[upgradeTarget] = Mathf.Min(_jobLevel[upgradeTarget] + 1, MaxUpgradeLevel);

        return true;
    }
}
