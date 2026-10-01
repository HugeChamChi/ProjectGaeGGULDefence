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
/// 잔존 시트 3개를 병렬 로드합니다. 캐릭터·게임 설정은 에디터에서 시트값을 반영한 SO를 사용합니다.
/// _gameDataManager 로 접근합니다.
///
/// 담당 시트:
///   - 소환 등장 확률    (gid=1984586417)
///   - 보스 HP/경험치    (gid=1622284954)
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
    private const string GidTotem = "660087267";

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

    // ── 토템 데이터 ───────────────────────────────────────
    private readonly Dictionary<int, TotemSheetRow> _totemRows = new();

    public async UniTask LoadAllAsync(CancellationToken token = default)
    {
        IsLoaded = false;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var (spawnCsv, bossCsv, totemCsv) = await UniTask.WhenAll(
                    FetchCsvAsync(GidSpawnRate, token),
                    FetchCsvAsync(GidBoss, token),
                    FetchCsvAsync(GidTotem, token)
                );

                if (spawnCsv == null || bossCsv == null || totemCsv == null)
                {
                    Debug.LogWarning("[GameDataManager] 시트 다운로드 일부 실패. 3초 후 전체 재시도합니다...");
                    await UniTask.Delay(3000, cancellationToken: token);
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
                ParseTotemData(totemCsv);
                ValidateDebuffBindings(pendingDebuffs);
                if (pendingDebuffs != null) _debuffCatalog.Replace(pendingDebuffs);

                IsLoaded = true;
                OnLoaded?.Invoke();
                Debug.Log("[GameDataManager] 모든 시트 로드 및 파싱 완료");
                break; // 성공 시 무한루프 탈출
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameDataManager] 파싱 중 에러 발생: {ex.Message}\n3초 후 전체 재시도합니다...");
                await UniTask.Delay(3000, cancellationToken: token);
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
        void Validate(DebuffBinding? optional, string source)
        {
            if (!optional.HasValue || !optional.Value.IsConfigured) return;
            var binding = optional.Value;
            DebuffDefinition definition;
            bool found = definitions != null ? definitions.TryGetValue(binding.DebuffId, out definition) : _debuffCatalog.TryGet(binding.DebuffId, out definition);
            if (!found) throw new FormatException($"{source}: unknown debuff_id={binding.DebuffId}");
            if (binding.Trigger == DebuffTrigger.AffectedUnitBasicAttackAttempt && definition.Kind != DebuffKind.ArmorBreak)
                throw new FormatException($"{source}: attack-attempt binding must be ArmorBreak.");
        }
        foreach (var row in _totemRows.Values) Validate(row.DebuffBinding, $"Totem {row.TotemId}");
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

    // ── 토템 시트 파싱 ────────────────────────────────────
    // 헤더 행 기반으로 컬럼 위치를 자동 감지합니다.
    // 수치 컬럼은 정수 퍼센트(10 = 10%) 형식이므로 100으로 나눠 저장합니다.

    private void ParseTotemData(string csv)
    {
        _totemRows.Clear();
        var rows = SplitCsvRows(csv);
        if (rows.Count < 2) return;

        var headers = rows[0];

        // ── 컬럼 인덱스 감지 ──
        int iId = FindCol(headers, "totem_id");
        int iName = FindCol(headers, "totem_name");
        int iGrade = FindCol(headers, "grade");
        int iEffect = FindCol(headers, "effect");
        int iRange = FindCol(headers, "effect_range");
        int iDisabled = FindCol(headers, "attack_disabled_range");
        int iRotatable = FindCol(headers, "is_rotatable");
        int iAtkUp = FindCol(headers, "atk_increase_rate");
        int iAtkDown = FindCol(headers, "atk_decrease_rate");
        int iSpdUp = FindCol(headers, "attack_speed_increase_rate");
        int iSpdDown = FindCol(headers, "attack_speed_decrease_rate");
        int iCritCh = FindCol(headers, "critical_chance_rate");
        int iCritDmg = FindCol(headers, "critical_damage_rate");
        int iCooldown = FindCol(headers, "cooldown_decrease_rate");
        int iProj = FindCol(headers, "projectile_size_rate");
        int iFoodProd = FindCol(headers, "food_production_rate");
        int iFoodAmt = FindCol(headers, "food_amount");
        int iExpGain = FindCol(headers, "exp_gain_rate");

        if (iId < 0)
        {
            Debug.LogWarning("[GameDataManager] 토템 시트: totem_id 컬럼 없음. 헤더 행 확인 필요.");
            return;
        }

        // is_rotatable 헤더가 없으면 위치 기반 fallback (시트의 7번째 컬럼)
        bool usePositionalRotatable = iRotatable < 0;
        if (usePositionalRotatable)
            Debug.LogWarning("[GameDataManager] 토템 시트: is_rotatable 헤더 없음. 컬럼 6번 위치로 fallback.");

        for (int i = 1; i < rows.Count; i++)
        {
            var cols = rows[i];
            if (cols.Length == 0 || iId >= cols.Length) continue;
            if (!int.TryParse(cols[iId], out int id)) continue;

            var row = new TotemSheetRow { TotemId = id };
            row.DebuffBinding = DebuffSheetParser.ParseBinding(headers, cols, false, $"Totem row {i + 1} id={id}");
            if (row.DebuffBinding.HasValue && row.DebuffBinding.Value.Trigger == DebuffTrigger.ProjectileHit)
            {
                int intervalColumn = DebuffSheetParser.Column(headers, "debuff_fire_interval_sec");
                int damageColumn = DebuffSheetParser.Column(headers, "debuff_impact_damage");
                if (intervalColumn < 0 || damageColumn < 0 || intervalColumn >= cols.Length || damageColumn >= cols.Length)
                    throw new FormatException($"Totem {id}: missing projectile settings.");
                row.DebuffFireInterval = double.Parse(cols[intervalColumn], NumberStyles.Float, CultureInfo.InvariantCulture);
                row.DebuffImpactDamage = decimal.Parse(cols[damageColumn], NumberStyles.Float, CultureInfo.InvariantCulture);
                if (row.DebuffFireInterval <= 0 || double.IsNaN(row.DebuffFireInterval.Value) || double.IsInfinity(row.DebuffFireInterval.Value) ||
                    row.DebuffImpactDamage < 0 || row.DebuffImpactDamage > CombatHealth.MaximumHp)
                    throw new FormatException($"Totem {id}: invalid projectile settings.");
            }

            if (iName >= 0 && iName < cols.Length) row.TotemName = cols[iName];
            if (iGrade >= 0 && iGrade < cols.Length) row.Grade = ParseTotemTier(cols[iGrade]);
            if (iEffect >= 0 && iEffect < cols.Length) row.Effect = cols[iEffect];
            if (iRange >= 0 && iRange < cols.Length) row.EffectRange = ParseOffsets(cols[iRange]);
            if (iDisabled >= 0 && iDisabled < cols.Length) row.AttackDisabledRange = ParseOffsets(cols[iDisabled]);

            // is_rotatable: 헤더 기반 → 없으면 위치 기반 (index 6)
            int rotCol = usePositionalRotatable ? 6 : iRotatable;
            if (rotCol >= 0 && rotCol < cols.Length)
            {
                var v = cols[rotCol].ToLowerInvariant();
                row.IsRotatable = v == "true" || v == "1" || v == "yes";
            }

            // 수치 컬럼: 정수 퍼센트(10 = 10%) → ÷100 → 소수 비율(0.1)
            TrySetPct(cols, iAtkUp, ref row.AtkIncreaseRate);
            TrySetPct(cols, iAtkDown, ref row.AtkDecreaseRate);
            TrySetPct(cols, iSpdUp, ref row.AttackSpeedIncreaseRate);
            TrySetPct(cols, iSpdDown, ref row.AttackSpeedDecreaseRate);
            TrySetPct(cols, iCritCh, ref row.CriticalChanceRate);
            TrySetPct(cols, iCritDmg, ref row.CriticalDamageRate);
            TrySetPct(cols, iCooldown, ref row.CooldownDecreaseRate);
            TrySetPct(cols, iProj, ref row.ProjectileSizeRate);
            TrySetPct(cols, iFoodProd, ref row.FoodProductionRate);
            TrySetFloat(cols, iFoodAmt, ref row.FoodAmount);
            TrySetPct(cols, iExpGain, ref row.ExpGainRate);

            _totemRows[id] = row;
        }

        Debug.Log($"[GameDataManager] 토템 시트 {_totemRows.Count}행 로드 완료");
    }

    // 정수 퍼센트
    private static void TrySetPct(string[] cols, int idx, ref float field)
    {
        if (idx < 0 || idx >= cols.Length) return;
        if (float.TryParse(cols[idx], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            field = v;
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
            "rare" or "레어" or "1" => Tier.Rare,
            "epic" or "에픽" or "2" => Tier.Epic,
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

    /// <summary>totemId 기준 시트 데이터 반환. 시트 미로드 또는 미등록은 null.</summary>
    public TotemSheetRow GetTotemRow(int totemId)
        => _totemRows.TryGetValue(totemId, out var row) ? row : null;

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

    /// <summary>
    /// 구글 시트 토템 행 데이터.
    /// 수치 필드는 소수 비율 형식 (0.1 = 10%). 파서에서 ÷100 처리됨.
    /// EffectRange / AttackDisabledRange: 리스트가 비어있으면 SO 데이터로 fallback.
    /// </summary>
    public class TotemSheetRow
    {
        public DebuffBinding? DebuffBinding;
        public double? DebuffFireInterval;
        public decimal? DebuffImpactDamage;
        public int TotemId;
        public string TotemName;
        public Tier Grade;
        public string Effect;
        public bool IsRotatable = true;

        // 범위 좌표 (+x=우 / -x=좌 / -y=상 / +y=하)
        public List<Vector2Int> EffectRange = new();
        public List<Vector2Int> AttackDisabledRange = new();

        // 버프/디버프 수치 (0.1 = 10%)
        public float AtkIncreaseRate;
        public float AtkDecreaseRate;
        public float AttackSpeedIncreaseRate;
        public float AttackSpeedDecreaseRate;
        public float CriticalChanceRate;
        public float CriticalDamageRate;
        public float CooldownDecreaseRate;
        public float ProjectileSizeRate;
        public float FoodProductionRate;
        public float FoodAmount;
        public float ExpGainRate;
    }

}
