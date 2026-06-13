using System;
using VContainer;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 시트에서 강화 데이터를 로드하고, 강화 상태·비용·스탯 조회를 제공합니다.
/// _upgradeManager 로 접근합니다.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
 
    public void Init()
    {

        
    }

    [Inject] private CurrencyManager _currencyManager;

    private const string StatSheetUrl = "https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/export?format=csv&gid=1454519483";

    private const int MaxUpgradeLevel = 10;

    public bool   IsLoaded { get; private set; }
    public event Action OnLoaded;
    private readonly Dictionary<int, CharacterStatRow[]> _statCache    = new();  // characterId → rows[level-1]
    private readonly Dictionary<int, string>          _charIdToJobType = new();

    private readonly Dictionary<string, int> _jobLevel = new()
    {
        { "Frog", 1 }, { "Frog_Gunner", 1 }, { "Frog_Ninja", 1 }, { "Frog_Wizard", 1 }, { "Frog_Chief", 1 }
    };
    private int _currencyLevel = 1;

    private async UniTaskVoid Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        await LoadAllAsync(token);
    }

    private async UniTask LoadAllAsync(CancellationToken token)
    {
        var statCsv = await FetchCsvAsync(StatSheetUrl, token);

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
            if (charType == "Frog_Magician") charType = "Frog_Wizard";

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

    public int GetUpgradeLevel(string upgradeTarget)
    {
        if (upgradeTarget == "Frog_Chief") return _currencyLevel;
        return _jobLevel.TryGetValue(upgradeTarget, out var lv) ? lv : 1;
    }

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
        int currentLevel = upgradeTarget == "Frog_Chief"
            ? _currencyLevel
            : (_jobLevel.TryGetValue(upgradeTarget, out var lv) ? lv : 1);

        if (currentLevel >= MaxUpgradeLevel) return -1;

        return 10; // 임시 고정 비용
    }

    /// <summary>강화를 시도합니다. 재화 부족 또는 최대 레벨이면 false.</summary>
    public bool TryUpgrade(string upgradeTarget)
    {
        int cost = GetUpgradeCost(upgradeTarget);
        if (cost < 0) return false;
        if (!_currencyManager.Spend(cost)) return false;

        if (upgradeTarget == "Frog_Chief")
            _currencyLevel = Mathf.Min(_currencyLevel + 1, MaxUpgradeLevel);
        else if (_jobLevel.ContainsKey(upgradeTarget))
            _jobLevel[upgradeTarget] = Mathf.Min(_jobLevel[upgradeTarget] + 1, MaxUpgradeLevel);

        return true;
    }
}
