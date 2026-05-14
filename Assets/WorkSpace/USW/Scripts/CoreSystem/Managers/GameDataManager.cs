using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 구글 시트 7개를 병렬 fetch하여 인게임 핵심 수치를 런타임에 제공합니다.
/// Manager.GameData 로 접근합니다.
///
/// 담당 시트:
///   - 소환 비용        (gid=1607115777)
///   - 소환 등장 확률    (gid=1984586417)
///   - 판매 가격        (gid=284585537)
///   - 재화 생산량       (gid=1326045266)
///   - 보스 HP/경험치    (gid=1622284954)
///   - 레벨업 경험치     (gid=499762901)
///   - 토템 데이터       (gid=660087267)
///
/// ─ 토템 시트 컬럼 규칙 ──────────────────────────────────────────────
///   수치 컬럼(atk_increase_rate 등)은 정수 퍼센트(10 = 10%) 저장.
///   파서에서 ÷100 하여 소수 비율(0.1 = 10%)로 변환.
///
///   effect_range / attack_disabled_range 형식: "x,y;x,y;..."
///     좌표 규칙:  +x=우(右) / -x=좌(左) / -y=상(上) / +y=하(下)
///     예) 상 1칸 → "0,-1"  /  상 3칸 → "0,-1;0,-2;0,-3"
///         좌우 → "-1,0;1,0"  /  비어있음 → ""
///
///   시트 헤더 행 예시 (컬럼 순서):
///     totem_id, totem_name, Local_key, grade, spawn_rate,
///     effect_range, is_rotatable, attack_disabled_range, effect,
///     atk_increase_rate, atk_decrease_rate,
///     attack_speed_increase_rate, attack_speed_decrease_rate,
///     critical_chance_rate, critical_damage_rate,
///     cooldown_decrease_rate, projectile_size_rate,
///     food_production_rate, food_amount, exp_gain_rate
/// </summary>
public class GameDataManager : InGameSingleton<GameDataManager>
{
    private const string BaseUrl = "https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/export?format=csv&gid=";

    private const string GidSummonCost = "1607115777";
    private const string GidSpawnRate  = "1984586417";
    private const string GidSellPrice  = "284585537";
    private const string GidCurrency   = "1326045266";
    private const string GidBoss       = "1622284954";
    private const string GidExpTable   = "499762901";
    private const string GidTotem      = "660087267";

    public bool   IsLoaded { get; private set; }
    public event Action OnLoaded;

    // ── 소환 비용 ─────────────────────────────────────────
    public int SummonInitialCost   { get; private set; } = 20;
    public int SummonCostIncrease  { get; private set; } = 20;

    // ── 소환 등장 확률 ────────────────────────────────────
    private readonly Dictionary<int, float> _spawnRates = new();
    private float _totalSpawnWeight;

    // ── 판매 가격 ─────────────────────────────────────────
    private readonly Dictionary<(int, int), int> _sellPrices = new();

    // ── 재화 생산량 ───────────────────────────────────────
    private readonly Dictionary<int, float> _currencyPerSecond = new();

    // ── 보스 데이터 ───────────────────────────────────────
    private readonly Dictionary<int, BossSheetRow> _bossByRoundId = new();
    private int _currentBossRoundId = -1;

    // ── 레벨업 경험치 ─────────────────────────────────────
    private float[] _expTable;

    // ── 토템 데이터 ───────────────────────────────────────
    private readonly Dictionary<int, TotemSheetRow> _totemRows = new();

    // ── 초기화 ────────────────────────────────────────────

    private async UniTaskVoid Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        await LoadAllAsync(token);
    }

    private async UniTask LoadAllAsync(CancellationToken token)
    {
        var (csv0, csv1, csv2, csv3, csv4, csv5, csv6) = await UniTask.WhenAll(
            FetchCsvAsync(GidSummonCost, token),
            FetchCsvAsync(GidSpawnRate,  token),
            FetchCsvAsync(GidSellPrice,  token),
            FetchCsvAsync(GidCurrency,   token),
            FetchCsvAsync(GidBoss,       token),
            FetchCsvAsync(GidExpTable,   token),
            FetchCsvAsync(GidTotem,      token)
        );

        if (csv0 != null) ParseSummonCost(csv0);
        if (csv1 != null) ParseSpawnRates(csv1);
        if (csv2 != null) ParseSellPrices(csv2);
        if (csv3 != null) ParseCurrencyPerSecond(csv3);
        if (csv4 != null) ParseBossData(csv4);
        if (csv5 != null) ParseExpTable(csv5);
        if (csv6 != null) ParseTotemData(csv6);

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

    // ── 기존 파싱 ─────────────────────────────────────────

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

    // ── 토템 시트 파싱 ────────────────────────────────────
    // 헤더 행 기반으로 컬럼 위치를 자동 감지합니다.
    // 수치 컬럼은 정수 퍼센트(10 = 10%) 형식이므로 100으로 나눠 저장합니다.

    private void ParseTotemData(string csv)
    {
        var lines = csv.Split('\n');
        if (lines.Length < 2) return;

        var headers = ParseCsvRow(lines[0]);

        // ── 컬럼 인덱스 감지 ──
        int iId        = FindCol(headers, "totem_id");
        int iName      = FindCol(headers, "totem_name");
        int iGrade     = FindCol(headers, "grade");
        int iEffect    = FindCol(headers, "effect");
        int iRange     = FindCol(headers, "effect_range");
        int iDisabled  = FindCol(headers, "attack_disabled_range");
        int iRotatable = FindCol(headers, "is_rotatable");
        int iAtkUp     = FindCol(headers, "atk_increase_rate");
        int iAtkDown   = FindCol(headers, "atk_decrease_rate");
        int iSpdUp     = FindCol(headers, "attack_speed_increase_rate");
        int iSpdDown   = FindCol(headers, "attack_speed_decrease_rate");
        int iCritCh    = FindCol(headers, "critical_chance_rate");
        int iCritDmg   = FindCol(headers, "critical_damage_rate");
        int iCooldown  = FindCol(headers, "cooldown_decrease_rate");
        int iProj      = FindCol(headers, "projectile_size_rate");
        int iFoodProd  = FindCol(headers, "food_production_rate");
        int iFoodAmt   = FindCol(headers, "food_amount");
        int iExpGain   = FindCol(headers, "exp_gain_rate");

        if (iId < 0)
        {
            Debug.LogWarning("[GameDataManager] 토템 시트: totem_id 컬럼 없음. 헤더 행 확인 필요.");
            return;
        }

        // is_rotatable 헤더가 없으면 위치 기반 fallback (시트의 7번째 컬럼)
        bool usePositionalRotatable = iRotatable < 0;
        if (usePositionalRotatable)
            Debug.LogWarning("[GameDataManager] 토템 시트: is_rotatable 헤더 없음. 컬럼 6번 위치로 fallback.");

        for (int i = 1; i < lines.Length; i++)
        {
            var cols = ParseCsvRow(lines[i]);
            if (cols.Length == 0 || iId >= cols.Length) continue;
            if (!int.TryParse(cols[iId], out int id)) continue;

            var row = new TotemSheetRow { TotemId = id };

            if (iName     >= 0 && iName     < cols.Length) row.TotemName    = cols[iName];
            if (iGrade    >= 0 && iGrade    < cols.Length) row.Grade        = ParseTotemTier(cols[iGrade]);
            if (iEffect   >= 0 && iEffect   < cols.Length) row.Effect       = cols[iEffect];
            if (iRange    >= 0 && iRange    < cols.Length) row.EffectRange  = ParseOffsets(cols[iRange]);
            if (iDisabled >= 0 && iDisabled < cols.Length) row.AttackDisabledRange = ParseOffsets(cols[iDisabled]);

            // is_rotatable: 헤더 기반 → 없으면 위치 기반 (index 6)
            int rotCol = usePositionalRotatable ? 6 : iRotatable;
            if (rotCol >= 0 && rotCol < cols.Length)
            {
                var v = cols[rotCol].ToLowerInvariant();
                row.IsRotatable = v == "true" || v == "1" || v == "yes";
            }

            // 수치 컬럼: 정수 퍼센트(10 = 10%) → ÷100 → 소수 비율(0.1)
            TrySetPct(cols, iAtkUp,    ref row.AttackBuff);
            TrySetPct(cols, iAtkDown,  ref row.AttackDebuff);
            TrySetPct(cols, iSpdUp,    ref row.SpeedBuff);
            TrySetPct(cols, iSpdDown,  ref row.SpeedDebuff);
            TrySetPct(cols, iCritCh,   ref row.CritChanceBuff);
            TrySetPct(cols, iCritDmg,  ref row.CritDamageBuff);
            TrySetPct(cols, iCooldown, ref row.CooldownDecrease);
            TrySetPct(cols, iProj,     ref row.ProjectileSizeRate);
            TrySetPct(cols, iFoodProd, ref row.FoodProductionBuff);
            TrySetFloat(cols, iFoodAmt,  ref row.FoodAmountBuff);
            TrySetPct(cols, iExpGain,  ref row.ExpGainRate);

            _totemRows[id] = row;
        }

        Debug.Log($"[GameDataManager] 토템 시트 {_totemRows.Count}행 로드 완료");
    }

    // 정수 퍼센트 → ÷100 소수 비율
    private static void TrySetPct(string[] cols, int idx, ref float field)
    {
        if (idx < 0 || idx >= cols.Length) return;
        if (float.TryParse(cols[idx], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            field = v / 100f;
    }

    // 소수 그대로 저장 (food_amount는 절댓값이므로 ÷100 불필요)
    private static void TrySetFloat(string[] cols, int idx, ref float field)
    {
        if (idx < 0 || idx >= cols.Length) return;
        if (float.TryParse(cols[idx], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            field = v;
    }

    /// <summary>
    /// "x,y;x,y;..." 형식의 오프셋 문자열을 Vector2Int 목록으로 파싱합니다.
    /// 빈 문자열이거나 좌표 형식이 아니면 빈 리스트를 반환합니다.
    /// 좌표 규칙: +x=우 / -x=좌 / -y=상 / +y=하
    /// </summary>
    public static List<Vector2Int> ParseOffsets(string raw)
    {
        var result = new List<Vector2Int>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        // 숫자나 '-'로 시작하지 않으면 한글 텍스트 등 비좌표 값 — 무시
        raw = raw.Trim();
        if (raw.Length == 0 || (!char.IsDigit(raw[0]) && raw[0] != '-')) return result;

        foreach (var pair in raw.Split(';'))
        {
            var xy = pair.Trim().Split(',');
            if (xy.Length != 2) continue;
            if (int.TryParse(xy[0].Trim(), out int x) &&
                int.TryParse(xy[1].Trim(), out int y))
                result.Add(new Vector2Int(x, y));
        }
        return result;
    }

    /// <summary>
    /// 따옴표로 묶인 필드(콤마 포함 가능)를 올바르게 파싱하는 CSV 행 분리기.
    /// "value","with, comma","escaped ""quote""" 형식 지원.
    /// </summary>
    public static string[] ParseCsvRow(string line)
    {
        line = line.TrimEnd('\r');
        var fields  = new List<string>();
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

    private static int FindCol(string[] headers, string name)
    {
        for (int i = 0; i < headers.Length; i++)
            if (string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static Tier ParseTotemTier(string grade) =>
        grade.Trim().ToLowerInvariant() switch
        {
            "normal" or "노말" or "common" or "0" => Tier.Normal,
            "rare"   or "레어" or "1"              => Tier.Rare,
            "epic"   or "에픽" or "2"              => Tier.Epic,
            "legend" or "legendary" or "전설" or "special" or "3" => Tier.Legend,
            _ => Tier.Normal,
        };

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

    public int GetSellPrice(int characterId, int upgradeLevel)
    {
        int clampedLevel = Mathf.Clamp(upgradeLevel, 1, 10);
        return _sellPrices.TryGetValue((characterId, clampedLevel), out var price) ? price : 0;
    }

    public float GetCurrencyPerSecond(int characterId)
        => _currencyPerSecond.TryGetValue(characterId, out var cps) ? cps : 0f;

    public int GetBossMaxHp(int roundId, int fallback = 1000)
        => _bossByRoundId.TryGetValue(roundId, out var row) ? row.MaxHealth : fallback;

    public float GetExpMultiplierForRound(int roundId)
    {
        if (!_bossByRoundId.TryGetValue(roundId, out var row)) return 0.01f;
        return row.DropExpAmount / row.DropExpPerHealth;
    }

    public void SetCurrentBossRound(int roundId) => _currentBossRoundId = roundId;

    public float GetCurrentExpMultiplier()
        => _currentBossRoundId >= 0 ? GetExpMultiplierForRound(_currentBossRoundId) : 0.01f;

    public float GetExpRequired(int level)
    {
        if (_expTable == null || _expTable.Length == 0) return 100f;
        int idx = Mathf.Clamp(level - 1, 0, _expTable.Length - 1);
        return _expTable[idx];
    }

    /// <summary>totemId 기준 시트 데이터 반환. 시트 미로드 또는 미등록은 null.</summary>
    public TotemSheetRow GetTotemRow(int totemId)
        => _totemRows.TryGetValue(totemId, out var row) ? row : null;

    // ── 내부 데이터 클래스 ────────────────────────────────

    private class BossSheetRow
    {
        public int   BossId;
        public int   RoundId;
        public int   MaxHealth;
        public float DropExpPerHealth;
        public float DropExpAmount;
    }

    /// <summary>
    /// 구글 시트 토템 행 데이터.
    /// 수치 필드는 소수 비율 형식 (0.1 = 10%). 파서에서 ÷100 처리됨.
    /// EffectRange / AttackDisabledRange: 리스트가 비어있으면 SO 데이터로 fallback.
    /// </summary>
    public class TotemSheetRow
    {
        public int    TotemId;
        public string TotemName;
        public Tier   Grade;
        public string Effect;
        public bool   IsRotatable = true;

        // 범위 좌표 (+x=우 / -x=좌 / -y=상 / +y=하)
        public List<Vector2Int> EffectRange         = new();
        public List<Vector2Int> AttackDisabledRange = new();

        // 버프/디버프 수치 (0.1 = 10%)
        public float AttackBuff;
        public float AttackDebuff;
        public float SpeedBuff;
        public float SpeedDebuff;
        public float CritChanceBuff;
        public float CritDamageBuff;
        public float CooldownDecrease;
        public float ProjectileSizeRate;
        public float FoodProductionBuff;
        public float FoodAmountBuff;
        public float ExpGainRate;
    }
}
