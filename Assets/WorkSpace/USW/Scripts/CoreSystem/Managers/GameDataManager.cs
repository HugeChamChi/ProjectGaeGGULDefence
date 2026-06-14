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
/// 구글 시트 7개를 병렬 fetch하여 인게임 핵심 수치를 런타임에 제공합니다.
/// _gameDataManager 로 접근합니다.
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
public class GameDataManager
{
 
    public void Init()
    {
    }

    private const string BaseUrl = "https://docs.google.com/spreadsheets/d/1gDHU35aPDHn2s4XiOch2s3Bl2s4iXF0rya37VMxmyiM/export?format=csv&gid=";

    private const string GidSpawnRate = "1984586417";
    private const string GidBoss = "1622284954";
    private const string GidExpTable = "499762901";
    private const string GidTotem = "660087267";
    private const string GidCharacter = "1454519483";
    private const string GidConfig = "2094930366";
    private const string GidLevelUp = "2005855251";
    private const string GidUpgrade = "842065624";

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

    // ── 레벨업 경험치 ─────────────────────────────────────
    private float[] _expTable;

    // ── 토템 데이터 ───────────────────────────────────────
    private readonly Dictionary<int, TotemSheetRow> _totemRows = new();

    // ── 캐릭터 데이터 (성장) ───────────────────────────────
    private readonly Dictionary<int, CharacterSheetRow> _characterData = new();

    // ── 업그레이드 데이터 ──────────────────────────────────
    private readonly Dictionary<int, UpgradeSheetRow> _upgradeRows = new();
    public class UpgradeSheetRow
    {
        public int Level;
        public float AtkIncreaseRate;
        public float AtkSpeedIncreaseRate;
        public int UpgradeCost;
    }

    public UpgradeSheetRow GetUpgradeRow(int level)
    {
        if (_upgradeRows.TryGetValue(level, out var row)) return row;
        return null;
    }

    // ── 레벨업 선택지 데이터 ───────────────────────────────
    private readonly Dictionary<int, LevelUpSheetRow> _levelUpRows = new();
    public class LevelUpSheetRow
    {
        public int ChooseId;
        public float SpawnRate;
        public string Description;
        public LevelUpEffectType PrimaryEffect;
        public float PrimaryValue;
        public LevelUpEffectType SecondaryEffect;
        public float SecondaryValue;
        public LevelUpSpecialEffect SpecialEffect;
        public float SpecialValue;
    }

    public LevelUpSheetRow GetLevelUpRow(int chooseId)
    {
        if (_levelUpRows.TryGetValue(chooseId, out var row)) return row;
        return null;
    }

    public async UniTask LoadAllAsync(CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var (csv0, csv1, csv2, csv3, csv4, csv5, csv6, csv7) = await UniTask.WhenAll(
                    FetchCsvAsync(GidSpawnRate, token),
                    FetchCsvAsync(GidBoss, token),
                    FetchCsvAsync(GidExpTable, token),
                    FetchCsvAsync(GidTotem, token),
                    FetchCsvAsync(GidCharacter, token),
                    FetchCsvAsync(GidConfig, token),
                    FetchCsvAsync(GidLevelUp, token),
                    FetchCsvAsync(GidUpgrade, token)
                );

                if (csv0 == null || csv1 == null || csv2 == null || csv3 == null || 
                    csv4 == null || csv5 == null || csv6 == null || csv7 == null)
                {
                    Debug.LogWarning("[GameDataManager] 시트 다운로드 일부 실패. 3초 후 전체 재시도합니다...");
                    await UniTask.Delay(3000, cancellationToken: token);
                    continue;
                }

                ParseSpawnRates(csv0);
                ParseBossData(csv1);
                ParseExpTable(csv2);
                ParseTotemData(csv3);
                ParseCharacterData(csv4);
                ParseWaveTime(csv5);
                ParseLevelUpData(csv6);
                ParseUpgradeData(csv7);

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


    private void ParseWaveTime(string csv)
    {
        var lines = csv.Split('\n');

        if (lines.Length >= 2)
        {
            var cols = lines[1].Trim().Split(',');

            if (cols.Length >= 2)
            {
                var data = RM.Load<GameConfig>("Data/GameConfig");

                if (float.TryParse(cols[0].Trim(), out var waveTime))
                {
                    data.countdownSeconds = waveTime;
                }

                if (int.TryParse(cols[1].Trim(), out var startFood))
                {
                    data.startingFood = startFood;
                }
            }
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
                BossId = bossId,
                RoundId = roundId,
                MaxHealth = hp,
                DropExpPerHealth = expPerHp,
                DropExpAmount = expAmt,
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

    public float GetCurrencyPerSecond(int characterId)
    {
        var row = GetCharacterRow(characterId);
        return row != null ? row.FoodProduction : 0f;
    }

    public int GetBossMaxHp(int roundId, int fallback = 1000)
        => _bossByRoundId.TryGetValue(roundId, out var row) ? row.MaxHealth : fallback;

    public float GetExpMultiplierForRound(int roundId)
    {
        if (!_bossByRoundId.TryGetValue(roundId, out var row) || row.MaxHealth <= 0) return 0.01f;
        
        // (DropExpAmount / DropExpPerHealth)는 보스를 100% 잡았을 때 줄 총 경험치 양입니다.
        // 이를 MaxHealth로 나누어 데미지 1당 줄 경험치 수치를 계산합니다.
        float totalExpForBoss = row.DropExpAmount / Mathf.Max(row.DropExpPerHealth, 0.001f);
        return totalExpForBoss / row.MaxHealth;
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

    /// <summary>캐릭터ID 기준 시트 데이터 반환</summary>
    public CharacterSheetRow GetCharacterRow(int charId)
        => _characterData.TryGetValue(charId, out var row) ? row : null;

    // ── 캐릭터 데이터 파싱 ──────────────────────────────────

    private void ParseCharacterData(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;

        var rows = SplitCsvRows(csv);
        
        for (int i = 2; i < rows.Count; i++)
        {
            var cols = rows[i];
            if (cols.Length < 10 || string.IsNullOrWhiteSpace(cols[0])) continue;

            if (!int.TryParse(cols[0], out int id)) continue;

            var row = new CharacterSheetRow
            {
                CharacterId = id,
                Name = cols[1],
                LocalKey = cols[2],
                CharacterType = cols[3],
                Grade = cols[4],
                Level = GradeToLevel(cols[4]),
                Atk = ParseFloat(cols[5]),
                AttackSpeed = ParseFloat(cols[6]),
                CriticalDamage = ParseFloat(cols[7]),
                CriticalChance = ParseFloat(cols[8]),
                FoodProduction = ParseFloat(cols[9])
            };

            Debug.Log($"[GameDataManager] Parsed Character {id} - Name: {row.Name}, FoodProduction: {row.FoodProduction}");

            _characterData[id] = row;
        }
        Debug.Log($"[GameDataManager] 캐릭터 데이터 {_characterData.Count}행 로드 완료");
    }

    private static int GradeToLevel(string grade)
    {
        return grade.Trim().ToLowerInvariant() switch
        {
            "normal" or "노말" or "0" or "common" => 1,
            "rare" or "레어" or "1" => 2,
            "epic" or "에픽" or "2" => 3,
            "legend" or "전설" or "3" or "special" => 4,
            _ => 1
        };
    }

    private static float ParseFloat(string v)
        => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f;

    // ── 내부 데이터 클래스 ────────────────────────────────

    private class BossSheetRow
    {
        public int BossId;
        public int RoundId;
        public int MaxHealth;
        public float DropExpPerHealth;
        public float DropExpAmount;
    }

    public class CharacterSheetRow
    {
        public int CharacterId;
        public string Name;
        public string LocalKey;
        public string CharacterType;
        public string Grade;
        public int Level;
        public float Atk;
        public float AttackSpeed;
        public float CriticalDamage;
        public float CriticalChance;
        public int SkillId;
        public float FoodProduction;
    }

    /// <summary>
    /// 구글 시트 토템 행 데이터.
    /// 수치 필드는 소수 비율 형식 (0.1 = 10%). 파서에서 ÷100 처리됨.
    /// EffectRange / AttackDisabledRange: 리스트가 비어있으면 SO 데이터로 fallback.
    /// </summary>
    public class TotemSheetRow
    {
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

    // ── 레벨업 시트 파싱 ────────────────────────────────────
    private void ParseLevelUpData(string csv)
    {
        var rows = SplitCsvRows(csv);
        if (rows.Count < 2) return;

        string[] headers = null;
        int headerIndex = -1;

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Length > 0 && rows[i][0].Trim() == "choose_id")
            {
                headers = rows[i];
                headerIndex = i;
                break;
            }
        }

        if (headers == null)
        {
            Debug.LogWarning("[GameDataManager] 레벨업 시트: choose_id 헤더 행을 찾을 수 없습니다.");
            return;
        }
        
        int iId = FindCol(headers, "choose_id");
        int iSpawn = FindCol(headers, "spawn_rate");
        int iDesc = FindCol(headers, "effect");
        int iAtk = FindCol(headers, "atk_increase");
        int iAtkSpd = FindCol(headers, "atk_speed_increase");
        int iCritDmg = FindCol(headers, "critical_damege_increase");
        int iCritChc = FindCol(headers, "critical_chance_increase");
        int iCool = FindCol(headers, "cooltime_decrease");
        int iFood = FindCol(headers, "food_production_rate");
        int iExp = FindCol(headers, "exp_gain_rate");
        int iAtkDec = FindCol(headers, "atk_decrease");
        int iAtkSpdDec = FindCol(headers, "atk_speed_decrease");

        if (iId < 0)
        {
            Debug.LogWarning("[GameDataManager] 레벨업 시트: choose_id 컬럼 인덱스 오류.");
            return;
        }

        for (int i = headerIndex + 1; i < rows.Count; i++)
        {
            var cols = rows[i];
            if (cols.Length <= iId || !int.TryParse(cols[iId], out int id)) continue;

            float spawn = iSpawn >= 0 && cols.Length > iSpawn ? ParseFloat(cols[iSpawn]) : 0f;
            string desc = iDesc >= 0 && cols.Length > iDesc ? cols[iDesc].Replace("\"", "").Replace("{", "").Replace("}", "") : "";
            Debug.Log($"[LevelUp Parsing] ID: {id}, Desc: {desc}");
            float atkInc = iAtk >= 0 && cols.Length > iAtk ? ParseFloat(cols[iAtk]) : 0f;
            float atkSpdInc = iAtkSpd >= 0 && cols.Length > iAtkSpd ? ParseFloat(cols[iAtkSpd]) : 0f;
            float critDmgInc = iCritDmg >= 0 && cols.Length > iCritDmg ? ParseFloat(cols[iCritDmg]) : 0f;
            float critChcInc = iCritChc >= 0 && cols.Length > iCritChc ? ParseFloat(cols[iCritChc]) : 0f;
            float coolInc = iCool >= 0 && cols.Length > iCool ? ParseFloat(cols[iCool]) : 0f;
            float foodInc = iFood >= 0 && cols.Length > iFood ? ParseFloat(cols[iFood]) : 0f;
            float expInc = iExp >= 0 && cols.Length > iExp ? ParseFloat(cols[iExp]) : 0f;
            float atkDec = iAtkDec >= 0 && cols.Length > iAtkDec ? ParseFloat(cols[iAtkDec]) : 0f;
            float atkSpdDec = iAtkSpdDec >= 0 && cols.Length > iAtkSpdDec ? ParseFloat(cols[iAtkSpdDec]) : 0f;

            float finalAtk = atkInc - atkDec;
            float finalAtkSpd = atkSpdInc - atkSpdDec;

            LevelUpEffectType pe = LevelUpEffectType.None; float pv = 0;
            LevelUpEffectType se = LevelUpEffectType.None; float sv = 0;
            LevelUpSpecialEffect sp = LevelUpSpecialEffect.None; float spv = 0;

            Action<LevelUpEffectType, float> AddEffect = (type, val) => {
                if (pe == LevelUpEffectType.None) { pe = type; pv = val; }
                else if (se == LevelUpEffectType.None) { se = type; sv = val; }
            };

            if (finalAtk != 0) AddEffect(LevelUpEffectType.AttackPercent, finalAtk);
            if (finalAtkSpd != 0) AddEffect(LevelUpEffectType.AttackSpeedPercent, finalAtkSpd);
            if (critDmgInc != 0) AddEffect(LevelUpEffectType.CritDamagePercent, critDmgInc);
            if (critChcInc != 0) AddEffect(LevelUpEffectType.CritChancePercent, critChcInc);
            if (coolInc != 0) AddEffect(LevelUpEffectType.GaugeSpeedPercent, coolInc);
            if (foodInc != 0) AddEffect(LevelUpEffectType.FoodProductionPercent, foodInc);
            if (expInc != 0) AddEffect(LevelUpEffectType.ExpGainPercent, expInc);

            // Hardcoded special values
            switch(id)
            {
                case 3009: sp = LevelUpSpecialEffect.AttackEveryNHits; spv = 10f; break;
                case 3010: pe = LevelUpEffectType.TotemEfficiencyPercent; pv = 20f; break;
                case 3011: sp = LevelUpSpecialEffect.MergeKeepsTribe; break;
                case 3012: sp = LevelUpSpecialEffect.SummonFixedDiscount; spv = 10f; break;
                case 3013: sp = LevelUpSpecialEffect.SellBonusFood; spv = 20f; break;
                
                case 3106: pe = LevelUpEffectType.TotemEfficiencyPercent; pv = 30f; break;
                case 3107: sp = LevelUpSpecialEffect.TriggerTotemSelection; break;
                case 3108: sp = LevelUpSpecialEffect.AttackEveryNHits; spv = 5f; break;
                case 3109: sp = LevelUpSpecialEffect.SummonDealsDamage; spv = 500f; break;
                case 3110: sp = LevelUpSpecialEffect.SellDealsDamage; spv = 800f; break;
                
                case 3203: pe = LevelUpEffectType.ChieftainAttackPercent; pv = 50f; break;
                case 3204: pe = LevelUpEffectType.ChieftainFoodProductionPercent; pv = 300f; break;
                case 3205: sp = LevelUpSpecialEffect.RandomBonusAttack; spv = 30f; break;
                case 3206: sp = LevelUpSpecialEffect.SellGivesRandomUnit; spv = 30f; break;
                case 3207: pe = LevelUpEffectType.TotemEfficiencyPercent; pv = 50f; break;
                case 3208: sp = LevelUpSpecialEffect.SellDealsDamage; spv = 1500f; break;
                
                case 3304: sp = LevelUpSpecialEffect.ExtraAttackEveryAttack; break;
                case 3305: sp = LevelUpSpecialEffect.ChieftainGainOnSell; pv = 5f; sv = 10f; break;
                case 3306: sp = LevelUpSpecialEffect.SellDealsDamage; spv = 4000f; break;
            }

            _levelUpRows[id] = new LevelUpSheetRow
            {
                ChooseId = id,
                SpawnRate = spawn,
                Description = desc,
                PrimaryEffect = pe,
                PrimaryValue = pv,
                SecondaryEffect = se,
                SecondaryValue = sv,
                SpecialEffect = sp,
                SpecialValue = spv
            };
        }
        
        Debug.Log($"[GameDataManager] 레벨업 시트 {_levelUpRows.Count}행 로드 완료");
    }

    // ── 업그레이드 시트 파싱 ─────────────────────────────────
    private void ParseUpgradeData(string csv)
    {
        var rows = SplitCsvRows(csv);
        if (rows.Count < 2) return;

        string[] headers = null;
        int headerIndex = -1;

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Length > 0 && rows[i][0].Trim() == "level")
            {
                headers = rows[i];
                headerIndex = i;
                break;
            }
        }

        if (headers == null)
        {
            Debug.LogWarning("[GameDataManager] 업그레이드 시트: level 헤더 행을 찾을 수 없습니다.");
            return;
        }

        int iLevel = FindCol(headers, "level");
        int iAtk = FindCol(headers, "atk");
        int iAtkSpd = FindCol(headers, "atk_speed");
        int iCost = FindCol(headers, "UpgradeCost");

        for (int i = headerIndex + 2; i < rows.Count; i++) // Skip type row
        {
            var cols = rows[i];
            if (cols.Length <= iLevel || !int.TryParse(cols[iLevel], out int level)) continue;

            string atkStr = iAtk >= 0 && cols.Length > iAtk ? cols[iAtk].Replace("%", "").Trim() : "0";
            string spdStr = iAtkSpd >= 0 && cols.Length > iAtkSpd ? cols[iAtkSpd].Replace("%", "").Trim() : "0";
            
            float atkInc = float.TryParse(atkStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float a) ? a : 0f;
            float spdInc = float.TryParse(spdStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) ? s : 0f;
            int cost = iCost >= 0 && cols.Length > iCost && int.TryParse(cols[iCost].Trim(), out int c) ? c : 0;

            _upgradeRows[level] = new UpgradeSheetRow
            {
                Level = level,
                AtkIncreaseRate = atkInc,
                AtkSpeedIncreaseRate = spdInc,
                UpgradeCost = cost
            };
        }
        Debug.Log($"[GameDataManager] 업그레이드 데이터 {_upgradeRows.Count}행 로드 완료");
    }
}
