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
    private int _session;

    /// <summary>Invalidates a row from a previous account before starting a new session.</summary>
    public void ClearSession() { _session++; _inDate = null; _owner = null; }

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
                catch (Exception error) { Debug.LogWarning("PlayerData format: " + error.GetType().Name); onComplete?.Invoke(null); return; }
                _inDate = rows[0]["inDate"].ToString();
                _owner = owner;
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
        var newData = new PlayerData
        {
            PlayerName = Backend.UserNickName ?? "유저",
            Gold = Mathf.Max(0, economy.InitialGold),
            Diamond = Mathf.Max(0, economy.InitialDiamond),
            Stamina = DefaultMaxStamina,
            MaxStamina = DefaultMaxStamina,
            LastStaminaRecoveryTime = ServerClock.UtcNowSeconds,
            PlayerLevel = 1,
            PlayerExp = 0,
            MaxExp = 500
        };

        Param param = PlayerDataToParam(newData);
        Backend.GameData.Insert(TABLE_NAME, param, bro =>
        {
            if (session != _session || owner != Backend.UserInDate) { onComplete?.Invoke(null); return; }
            if (bro.IsSuccess())
            {
                _inDate = bro.GetInDate();
                _owner = owner;
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
        if (data == null || string.IsNullOrEmpty(_inDate) || _owner != Backend.UserInDate)
        {
            Debug.LogError("저장할 데이터가 없습니다");
            onComplete?.Invoke();
            onResult?.Invoke(false);
            return;
        }

        Param param = PlayerDataToParam(data);
        string owner = _owner;
        int session = _session;
        Backend.GameData.UpdateV2(TABLE_NAME, _inDate, owner, param, bro =>
        {
            if (session != _session || owner != Backend.UserInDate) { onResult?.Invoke(false); onComplete?.Invoke(); return; }
            if (bro.IsSuccess())
            {
                Debug.Log("PlayerData 저장 성공");
            }
            else
            {
                Debug.LogError("PlayerData 저장 실패 : " + bro);
            }
            onComplete?.Invoke();
            onResult?.Invoke(bro.IsSuccess());
        });
    }

    // -------------------------
    // 파싱 헬퍼
    // -------------------------
    private PlayerData ParsePlayerData(JsonData data)
    {
        return new PlayerData
        {
            PlayerUid = Backend.UID,
            SaveId = data.GetString("PlayerSaveId", string.Empty),
            PendingEntryId = data.GetString("PendingEntryId", string.Empty),
            PendingEntryCost = data.GetInt("PendingEntryCost", 0),
            CouponHistory = JsonMapper.ToObject<System.Collections.Generic.List<CouponReceipt>>(data.GetString("CouponHistory", "[]")),
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
            AttendanceCount = data.GetInt(COLUMN_ATTENDANCE_COUNT, 0),
            AttendanceLastDay = data.GetString(COLUMN_ATTENDANCE_LAST_DAY, string.Empty),
            ShowDamageNumbers = data.GetInt(COLUMN_SHOW_DAMAGE_NUMBERS, 1) != 0,
            VibrationEnabled = data.GetInt(COLUMN_VIBRATION_ENABLED, 1) != 0
        };
    }

    private Param PlayerDataToParam(PlayerData data)
    {
        Param param = new Param();
        param.Add("PlayerSaveId", data.SaveId ?? string.Empty);
        param.Add("PendingEntryId", data.PendingEntryId ?? string.Empty);
        param.Add("PendingEntryCost", data.PendingEntryCost);
        param.Add("CouponHistory", JsonMapper.ToJson(data.CouponHistory ?? new System.Collections.Generic.List<CouponReceipt>()));
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
        param.Add(COLUMN_ATTENDANCE_COUNT, data.AttendanceCount);
        param.Add(COLUMN_ATTENDANCE_LAST_DAY, data.AttendanceLastDay ?? string.Empty);
        param.Add(COLUMN_SHOW_DAMAGE_NUMBERS, data.ShowDamageNumbers ? 1 : 0);
        param.Add(COLUMN_VIBRATION_ENABLED, data.VibrationEnabled ? 1 : 0);
        return param;
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
