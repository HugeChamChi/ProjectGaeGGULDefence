using System;
using UnityEngine;
using BackEnd;
using LitJson;
using Cysharp.Threading.Tasks;

public class BackendGameData
{
    private const string TABLE_NAME = "PlayerData";
    private const string COLUMN_PLAYER_NAME = "PlayerName";
    private const string COLUMN_GOLD = "Gold";
    private const string COLUMN_DIAMOND = "Diamond";
    private const string COLUMN_STAMINA = "Stamina";
    private const string COLUMN_MAX_STAMINA = "MaxStamina";
    private const string COLUMN_LAST_STAMINA_RECOVERY_TIME = "LastStaminaRecoveryTime";
    private const string COLUMN_PLAYER_LEVEL = "PlayerLevel";
    private const string COLUMN_PLAYER_EXP = "PlayerExp";
    private const string COLUMN_MAX_EXP = "MaxExp";
    private const string COLUMN_LAST_RESET_DATE = "LastResetDate";
    private const string COLUMN_SHOW_DAMAGE_NUMBERS = "ShowDamageNumbers";
    private const string COLUMN_VIBRATION_ENABLED = "VibrationEnabled";
    private const string COLUMN_ATTENDANCE_COUNT = "AttendanceCount";
    private const string COLUMN_ATTENDANCE_LAST_DAY = "AttendanceLastDay";
    private string _inDate;
    private string _owner;
    private string _serverSaveId;
    private bool _hasSaveId;
    private readonly System.Collections.Generic.HashSet<string> _submittedSaveIds = new();
    public bool HasWriteConflict { get; private set; }
    private int _session;

    /// <summary>Invalidates a row from a previous account before starting a new session.</summary>
    public void ClearSession() { _session++; _inDate = null; _owner = null; _serverSaveId = null; _hasSaveId = false; _submittedSaveIds.Clear(); HasWriteConflict = false; }

    // 기획 데이터(회복 주기/기본 최대치)는 Resources/StaminaConfig에서 읽어와 코드 수정 없이 조절 가능하게 한다.
    private static StaminaConfig _staminaConfig;
    private static StaminaConfig StaminaConfig
    {
        get
        {
            if (_staminaConfig == null)
            {
                _staminaConfig = RM.Load<StaminaConfig>("Data/StaminaConfig");
                if (_staminaConfig == null)
                    Debug.LogWarning("Addressables 'Data/StaminaConfig'를 찾을 수 없어 기본값(10분당 1, 최대 30)을 사용합니다.");
            }
            return _staminaConfig;
        }
    }

    private static int RecoveryIntervalSeconds => StaminaConfig != null ? StaminaConfig.recoveryIntervalSeconds : 600;
    private static int DefaultMaxStamina => StaminaConfig != null ? StaminaConfig.defaultMaxStamina : 30;

    // -------------------------
    // 데이터 불러오기
    // -------------------------
    public virtual void GameDataGet(Action<PlayerData> onComplete = null)
    {
        string owner = Backend.UserInDate;
        int session = _session;
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), bro =>
        {
            if (session != _session || owner != Backend.UserInDate) { onComplete?.Invoke(null); return; }
            if (bro.IsSuccess())
            {
                JsonData rows = bro.FlattenRows();

                if (rows.Count <= 0)
                {
                    Debug.Log("PlayerData 없음 → 신규 생성");
                    GameDataInsert(onComplete);
                    return;
                }

                if (rows.Count != 1) { Debug.LogWarning("PlayerData duplicate rows require repair."); onComplete?.Invoke(null); return; }
                PlayerData data;
                try { data = ParsePlayerData(rows[0]); }
                catch (Exception error) { Debug.LogWarning("PlayerData format: " + error.GetType().Name + ": " + error.Message); onComplete?.Invoke(null); return; }
                _inDate = rows[0]["inDate"].ToString();
                _owner = owner;
                _serverSaveId = data.SaveId ?? string.Empty;
                _hasSaveId = rows[0].ContainsKey("PlayerSaveId");
                onComplete?.Invoke(data);
            }
            else
            {
                Debug.LogError("PlayerData 불러오기 실패 : " + bro);
                onComplete?.Invoke(null);
            }
        });
    }

    // -------------------------
    // 데이터 최초 생성
    // -------------------------
    private void GameDataInsert(Action<PlayerData> onComplete = null)
    {
        string owner = Backend.UserInDate;
        int session = _session;
        var economy = Resources.Load<PlayerEconomyConfig>("PlayerEconomyConfig");
        if (economy == null) { Debug.LogError("PlayerEconomyConfig is missing."); onComplete?.Invoke(null); return; }
        var progression = Resources.Load<AccountProgressionConfig>("AccountProgressionConfig");
        if (progression == null) { Debug.LogError("AccountProgressionConfig is missing."); onComplete?.Invoke(null); return; }
        progression.Validate();
        var newData = new PlayerData
        {
            PlayerName = Backend.UserNickName ?? "유저",
            Gold = Mathf.Max(0, economy.InitialGold),
            Diamond = Mathf.Max(0, economy.InitialDiamond),
            Stamina = DefaultMaxStamina,
            MaxStamina = DefaultMaxStamina,
            LastStaminaRecoveryTime = ServerClock.UtcNowSeconds,
            PlayerLevel = progression.InitialLevel,
            PlayerExp = progression.InitialExperience,
            MaxExp = progression.RequiredExperience[progression.InitialLevel - 1]
        };

        Param param = PlayerDataToParam(newData);
        Backend.GameData.Insert(TABLE_NAME, param, bro =>
        {
            if (session != _session || owner != Backend.UserInDate) { onComplete?.Invoke(null); return; }
            if (bro.IsSuccess())
            {
                _inDate = bro.GetInDate();
                _owner = owner;
                _serverSaveId = newData.SaveId ?? string.Empty; _hasSaveId = true;
                Debug.Log("PlayerData 생성 성공");
                onComplete?.Invoke(newData);
            }
            else
            {
                Debug.LogError("PlayerData 생성 실패 : " + bro);
                onComplete?.Invoke(null);
            }
        });
    }

    // -------------------------
    // 데이터 저장
    // -------------------------
    public virtual void GameDataUpdate(PlayerData data, Action onComplete = null, Action<bool> onResult = null)
    {
        if (data == null || HasWriteConflict || string.IsNullOrEmpty(_inDate) || _owner != Backend.UserInDate)
        {
            Debug.LogError("저장할 데이터가 없습니다");
            onComplete?.Invoke();
            onResult?.Invoke(false);
            return;
        }

        Param param = PlayerDataToParam(data);
        string owner = _owner;
        int session = _session;
        string expected = _serverSaveId;
        _submittedSaveIds.Add(data.SaveId ?? string.Empty);
        void Complete(bool saved)
        {
            bool current = session == _session && owner == Backend.UserInDate;
            if (current && saved) { _serverSaveId = data.SaveId ?? string.Empty; _hasSaveId = true; _submittedSaveIds.Clear(); }
            onComplete?.Invoke(); onResult?.Invoke(current && saved);
        }
        void OnUpdated(BackendReturnObject bro)
        {
            if (session != _session || owner != Backend.UserInDate) { Complete(false); return; }
            if (bro.IsSuccess())
            {
                Debug.Log("PlayerData 저장 성공");
                Complete(true);
            }
            else if (bro.GetStatusCode() == "404")
            {
                // A lost ACK may mean this exact snapshot already committed. Never overwrite an unknown version.
                Backend.GameData.GetMyData(TABLE_NAME, new Where(), read =>
                {
                    if (session != _session || owner != Backend.UserInDate || !read.IsSuccess()) { Complete(false); return; }
                    var rows = read.FlattenRows();
                    string remote = rows.Count == 1 ? rows[0].GetString("PlayerSaveId", "") : null;
                    if (remote == data.SaveId) { Complete(true); return; }
                    if (remote != null && _submittedSaveIds.Contains(remote)) _serverSaveId = remote;
                    else if (remote != expected) HasWriteConflict = true;
                    Complete(false);
                });
            }
            else { Debug.LogWarning("PlayerData save failed: " + bro.GetStatusCode()); Complete(false); }
        }
        if (_hasSaveId)
        {
            var where = new Where(); where.Equal("PlayerSaveId", expected);
            Backend.GameData.Update(TABLE_NAME, where, param, OnUpdated);
        }
        else Backend.GameData.UpdateV2(TABLE_NAME, _inDate, owner, param, OnUpdated); // One-time legacy column bootstrap.
    }

    // -------------------------
    // 파싱 헬퍼
    // -------------------------
    public virtual async UniTask<bool> CommitCharacterUpgradeAsync(PlayerData next, string expectedSaveId, AccountJsonStore.Row row, string json)
    {
        string owner = _owner; int session = _session;
        if (string.IsNullOrEmpty(_inDate) || owner != Backend.UserInDate || string.IsNullOrEmpty(row.InDate) || expectedSaveId != _serverSaveId) return false;
        var playerWhere = new Where(); playerWhere.Equal("PlayerSaveId", expectedSaveId);
        var characterWhere = new Where(); characterWhere.Equal("Data", row.Json);
        var playerParam = new Param(); playerParam.Add("Gold", next.Gold); playerParam.Add("Diamond", next.Diamond); playerParam.Add("RewardInventory", JsonMapper.ToJson(next.RewardInventory)); playerParam.Add("PlayerSaveId", next.SaveId);
        var characterParam = new Param(); characterParam.Add("Data", json);
        var writes = new System.Collections.Generic.List<TransactionValue>
        {
            TransactionValue.SetUpdate("PlayerData", playerWhere, playerParam),
            TransactionValue.SetUpdate("PlayerCharacterData", characterWhere, characterParam)
        };
        var done = new UniTaskCompletionSource<bool>();
        Backend.GameData.TransactionWriteV2(writes, result => done.TrySetResult(result.IsSuccess()));
        bool saved = await done.Task;
        if (session != _session || owner != Backend.UserInDate) return false;
        if (saved) { _serverSaveId = next.SaveId; _hasSaveId = true; _submittedSaveIds.Clear(); }
        else HasWriteConflict = true;
        return saved;
    }

    private PlayerData ParsePlayerData(JsonData data)
    {
        // Missing legacy columns may use defaults; malformed existing amounts must never become zero saves.
        foreach (string column in new[] { COLUMN_GOLD, COLUMN_DIAMOND, COLUMN_STAMINA, COLUMN_MAX_STAMINA,
            COLUMN_PLAYER_LEVEL, COLUMN_PLAYER_EXP, COLUMN_MAX_EXP, COLUMN_ATTENDANCE_COUNT, "AttendancePendingDays", "PendingEntryCost" })
        {
            if (data.ContainsKey(column) && (!int.TryParse(data.GetString(column, null),
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int number) || number < 0))
                throw new FormatException("Invalid saved amount: " + column);
        }
        foreach (string column in new[] { COLUMN_LAST_STAMINA_RECOVERY_TIME, "AccountExperienceSequence" })
        {
            if (data.ContainsKey(column) && (!long.TryParse(data.GetString(column, null),
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long number) || number < 0))
                throw new FormatException("Invalid saved counter: " + column);
        }
        return new PlayerData
        {
            UpgradeProgress = ParseUpgradeProgress(data.GetString("UpgradeProgressJson", "{\"SchemaVersion\":1,\"Trees\":[],\"LocalImportId\":\"\"}")),
            PlayerUid = Backend.UID,
            SaveId = data.GetString("PlayerSaveId", string.Empty),
            WeeklyShopPurchasePeriod = data.GetString("WeeklyShopPurchasePeriod", string.Empty),
            WeeklyShopPurchases = JsonMapper.ToObject<System.Collections.Generic.List<int>>(data.GetString("WeeklyShopPurchases", "[]")),
            ShopPurchaseDay = data.GetString("ShopPurchaseDay", string.Empty),
            ShopPurchases = JsonMapper.ToObject<System.Collections.Generic.List<int>>(data.GetString("ShopPurchases", "[]")),
            RoundRewardClaims = JsonMapper.ToObject<System.Collections.Generic.List<string>>(data.GetString("RoundRewardClaims", "[]")) ?? new(),
            CompletedTutorials = JsonMapper.ToObject<System.Collections.Generic.List<string>>(data.GetString("CompletedTutorials", "[]")),
            SelectedChiefId = data.GetInt("SelectedChiefId", -1),
            SelectedIconId = data.GetInt("SelectedIconId", -1),
            SelectedFrameId = data.GetInt("SelectedFrameId", -1),
            UnitLevels = JsonMapper.ToObject<System.Collections.Generic.List<PlayerData.UnitLevelEntry>>(data.GetString("UnitLevels", "[]")),
            RewardInventory = JsonMapper.ToObject<RewardInventory>(data.GetString("RewardInventory", "{}")),
            AccountExperienceSequence = data.GetLong("AccountExperienceSequence", 0),
            AccountExperienceRunId = data.GetString("AccountExperienceRunId", string.Empty),
            AccountExperienceReceipt = ParseAccountExperienceReceipt(data.GetString("AccountExperienceReceipt", string.Empty)),
            PendingEntryId = data.GetString("PendingEntryId", string.Empty),
            PendingEntryCost = data.GetInt("PendingEntryCost", 0),
            CouponHistory = ParseCouponHistory(data.GetString("CouponHistory", "[]")),
            DiamondPurchases = ParseDiamondPurchases(data.GetString("DiamondPurchases", "[]")),
            PlayerName = data.GetString(COLUMN_PLAYER_NAME, "유저"),
            Gold       = data.GetInt(COLUMN_GOLD, 0),
            Diamond    = data.GetInt(COLUMN_DIAMOND, 0),
            Stamina    = data.GetInt(COLUMN_STAMINA, DefaultMaxStamina),
            MaxStamina = data.GetInt(COLUMN_MAX_STAMINA, DefaultMaxStamina),
            LastStaminaRecoveryTime = data.GetLong(COLUMN_LAST_STAMINA_RECOVERY_TIME, ServerClock.UtcNowSeconds),
            PlayerLevel = data.GetInt(COLUMN_PLAYER_LEVEL, 1),
            PlayerExp   = data.GetInt(COLUMN_PLAYER_EXP, 0),
            MaxExp      = data.GetInt(COLUMN_MAX_EXP, 500),
            LastResetDate = data.GetString(COLUMN_LAST_RESET_DATE, string.Empty),
            EventMissions = JsonMapper.ToObject<System.Collections.Generic.List<GGD.Events.SavedMission>>(data.GetString("EventMissions", "[]")),
            EventMissionReceipts = JsonMapper.ToObject<System.Collections.Generic.List<string>>(data.GetString("EventMissionReceipts", "[]")),
            EventAttendance = JsonMapper.ToObject<System.Collections.Generic.List<GGD.Events.AttendanceEventProgress>>(data.GetString("EventAttendance", "[]")),
            AttendanceCount = data.GetInt(COLUMN_ATTENDANCE_COUNT, 0),
            AttendancePendingDays = data.GetInt("AttendancePendingDays", 0),
            AttendanceLastVisitDay = data.GetString("AttendanceLastVisitDay", string.Empty),
            AttendanceLastDay = data.GetString(COLUMN_ATTENDANCE_LAST_DAY, string.Empty),
            ShowDamageNumbers = data.GetInt(COLUMN_SHOW_DAMAGE_NUMBERS, 1) != 0,
            VibrationEnabled = data.GetInt(COLUMN_VIBRATION_ENABLED, 1) != 0
        };
    }

    private Param PlayerDataToParam(PlayerData data)
    {
        data.UpgradeProgress.Validate();
        Param param = new Param();
        // LitJson serializes ResearchSaveData.CurrentVersion (a const), then cannot deserialize it.
        param.Add("UpgradeProgressJson", JsonUtility.ToJson(data.UpgradeProgress));
        param.Add("PlayerSaveId", data.SaveId ?? string.Empty);
        param.Add("WeeklyShopPurchasePeriod", data.WeeklyShopPurchasePeriod ?? string.Empty);
        param.Add("WeeklyShopPurchases", JsonMapper.ToJson(data.WeeklyShopPurchases));
        param.Add("ShopPurchaseDay", data.ShopPurchaseDay ?? string.Empty);
        param.Add("ShopPurchases", JsonMapper.ToJson(data.ShopPurchases));
        param.Add("RoundRewardClaims", JsonMapper.ToJson(data.RoundRewardClaims));
        param.Add("CompletedTutorials", JsonMapper.ToJson(data.CompletedTutorials));
        param.Add("SelectedChiefId", data.SelectedChiefId);
        param.Add("SelectedIconId", data.SelectedIconId);
        param.Add("SelectedFrameId", data.SelectedFrameId);
        param.Add("UnitLevels", JsonMapper.ToJson(data.UnitLevels ?? new System.Collections.Generic.List<PlayerData.UnitLevelEntry>()));
        param.Add("RewardInventory", JsonMapper.ToJson(data.RewardInventory));
        param.Add("AccountExperienceSequence", data.AccountExperienceSequence);
        param.Add("AccountExperienceRunId", data.AccountExperienceRunId ?? string.Empty);
        param.Add("AccountExperienceReceipt", IsEmptyExperienceReceipt(data.AccountExperienceReceipt) ? string.Empty : JsonMapper.ToJson(data.AccountExperienceReceipt));
        param.Add("PendingEntryId", data.PendingEntryId ?? string.Empty);
        param.Add("PendingEntryCost", data.PendingEntryCost);
        param.Add("CouponHistory", JsonMapper.ToJson(data.CouponHistory ?? new System.Collections.Generic.List<CouponReceipt>()));
        param.Add("DiamondPurchases", JsonMapper.ToJson(data.DiamondPurchases ?? new System.Collections.Generic.List<GaeGGUL.IAP.DiamondPurchaseReceipt>()));
        param.Add(COLUMN_PLAYER_NAME, data.PlayerName);
        param.Add(COLUMN_GOLD, data.Gold);
        param.Add(COLUMN_DIAMOND, data.Diamond);
        param.Add(COLUMN_STAMINA, data.Stamina);
        param.Add(COLUMN_MAX_STAMINA, data.MaxStamina);
        param.Add(COLUMN_LAST_STAMINA_RECOVERY_TIME, data.LastStaminaRecoveryTime);
        param.Add(COLUMN_PLAYER_LEVEL, data.PlayerLevel);
        param.Add(COLUMN_PLAYER_EXP, data.PlayerExp);
        param.Add(COLUMN_MAX_EXP, data.MaxExp);
        param.Add(COLUMN_LAST_RESET_DATE, data.LastResetDate);
        param.Add("EventMissions", JsonMapper.ToJson(data.EventMissions));
        param.Add("EventMissionReceipts", JsonMapper.ToJson(data.EventMissionReceipts));
        param.Add("EventAttendance", JsonMapper.ToJson(data.EventAttendance ?? new System.Collections.Generic.List<GGD.Events.AttendanceEventProgress>()));
        param.Add(COLUMN_ATTENDANCE_COUNT, data.AttendanceCount);
        param.Add("AttendancePendingDays", data.AttendancePendingDays);
        param.Add("AttendanceLastVisitDay", data.AttendanceLastVisitDay ?? string.Empty);
        param.Add(COLUMN_ATTENDANCE_LAST_DAY, data.AttendanceLastDay ?? string.Empty);
        param.Add(COLUMN_SHOW_DAMAGE_NUMBERS, data.ShowDamageNumbers ? 1 : 0);
        param.Add(COLUMN_VIBRATION_ENABLED, data.VibrationEnabled ? 1 : 0);
        return param;
    }

    private static UpgradeProgressData ParseUpgradeProgress(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Empty upgrade save.");
        var root = JsonMapper.ToObject(json);
        if (root == null || !root.IsObject || !root.ContainsKey("SchemaVersion") || !root.ContainsKey("Trees"))
            throw new FormatException("Incomplete upgrade save.");
        if (!root["SchemaVersion"].IsInt || root["Trees"] == null || !root["Trees"].IsArray)
            throw new FormatException("Invalid upgrade save fields.");
        var value = JsonUtility.FromJson<UpgradeProgressData>(json) ?? throw new FormatException("Null upgrade save.");
        value.Validate(); return value;
    }

    // Installed LitJson cannot deserialize a top-level null token. Missing/empty legacy columns are an absent receipt.
    private static AccountExperienceReceipt ParseAccountExperienceReceipt(string json)
    {
        json = json?.Trim();
        if (string.IsNullOrEmpty(json) || json == "null") return null;
        var receipt = JsonMapper.ToObject<AccountExperienceReceipt>(json);
        // JsonUtility materializes null inline classes as all-default objects when cloning save snapshots.
        // Treat only that exact empty shape as absent; retain validation for partially populated receipts.
        if (IsEmptyExperienceReceipt(receipt)) return null;
        if (string.IsNullOrEmpty(receipt.RunId) || receipt.Sequence <= 0 || receipt.Experience < 0)
            throw new FormatException("Invalid account experience receipt.");
        return receipt;
    }

    private static bool IsEmptyExperienceReceipt(AccountExperienceReceipt receipt) => receipt == null ||
        (string.IsNullOrEmpty(receipt.RunId) && receipt.Sequence == 0 && receipt.Experience == 0 && receipt.LevelBefore == 0 && receipt.LevelAfter == 0);

    private static System.Collections.Generic.List<CouponReceipt> ParseCouponHistory(string json)
    {
        json = json?.Trim();
        if (string.IsNullOrEmpty(json) || json == "null") return new System.Collections.Generic.List<CouponReceipt>();
        var history = JsonMapper.ToObject<System.Collections.Generic.List<CouponReceipt>>(json);
        if (history == null) throw new FormatException("Invalid coupon history.");
        foreach (var receipt in history)
            if (receipt == null || string.IsNullOrEmpty(receipt.Id) || receipt.Gold < 0 || receipt.Diamond < 0 || receipt.Stamina < 0)
                throw new FormatException("Invalid coupon history entry; existing deduplication history must be repaired.");
        return history;
    }

    private static System.Collections.Generic.List<GaeGGUL.IAP.DiamondPurchaseReceipt> ParseDiamondPurchases(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null") return new();
        var history = JsonMapper.ToObject<System.Collections.Generic.List<GaeGGUL.IAP.DiamondPurchaseReceipt>>(json);
        if (history == null) throw new FormatException("Invalid diamond purchase history.");
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var receipt in history)
            if (receipt == null || string.IsNullOrWhiteSpace(receipt.Id) || string.IsNullOrWhiteSpace(receipt.ProductId)
                || receipt.Diamond <= 0 || !ids.Add(receipt.Id))
                throw new FormatException("Invalid diamond purchase history; existing grants must be preserved.");
        return history;
    }

    // -------------------------
    // 스태미나 관련
    // -------------------------
    /// <summary>Recovery at <paramref name="now"/> (server Unix seconds) using the authored interval.</summary>
    public (int stamina, long recoveryTime) CalculateStaminaRecovery(int currentStamina, long lastRecoveryTime, int maxStamina, long now)
    {
        return StaminaRecovery.Calculate(currentStamina, lastRecoveryTime, maxStamina, Mathf.Max(1, RecoveryIntervalSeconds), now);
    }

    /// <summary>Seconds until the next recovery tick at <paramref name="now"/> (server Unix seconds).</summary>
    public int GetTimeUntilNextStaminaRecovery(long lastRecoveryTime, long now)
    {
        long elapsed = now - lastRecoveryTime;
        int recoveryInterval = Mathf.Max(1, RecoveryIntervalSeconds);
        int remaining = recoveryInterval - (int)(Math.Max(0, elapsed) % recoveryInterval);
        return remaining;
    }
}
