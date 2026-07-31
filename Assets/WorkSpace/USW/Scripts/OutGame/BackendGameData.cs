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
    private string _inDate;

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

    public BackendGameData()
    {
#if UNITY_EDITOR
        EditorInitBackend();
#endif
    }

#if UNITY_EDITOR
    private static bool _editorInitialized = false;

    private void EditorInitBackend()
    {
        if (_editorInitialized) return;
        _editorInitialized = true;

        var initBro = Backend.Initialize();
        if (!initBro.IsSuccess())
        {
            Debug.LogError("[Editor] Backend 초기화 실패: " + initBro);
            return;
        }

        var loginBro = Backend.BMember.CustomLogin("testuser", "testpass");
        if (!loginBro.IsSuccess())
        {
            loginBro = Backend.BMember.CustomSignUp("testuser", "testpass");
            if (!loginBro.IsSuccess())
                Debug.LogError("[Editor] Backend 로그인 실패: " + loginBro);
        }
    }
#endif

    // -------------------------
    // 데이터 불러오기
    // -------------------------
    public void GameDataGet(Action<PlayerData> onComplete = null)
    {
        Backend.GameData.GetMyData(TABLE_NAME, new Where(), bro =>
        {
            if (bro.IsSuccess())
            {
                JsonData rows = bro.FlattenRows();

                if (rows.Count <= 0)
                {
                    Debug.Log("PlayerData 없음 → 신규 생성");
                    GameDataInsert(onComplete);
                    return;
                }

                _inDate = rows[0]["inDate"].ToString();
                var data = ParsePlayerData(rows[0]);

                Debug.Log($"PlayerData 불러오기 성공 : {data.PlayerName}");
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
        var newData = new PlayerData
        {
            PlayerName = Backend.UserNickName ?? "유저",
            Gold = 0,
            Diamond = 0,
            Stamina = DefaultMaxStamina,
            MaxStamina = DefaultMaxStamina,
            LastStaminaRecoveryTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            PlayerLevel = 1,
            PlayerExp = 0,
            MaxExp = 500
        };

        Param param = PlayerDataToParam(newData);
        Backend.GameData.Insert(TABLE_NAME, param, bro =>
        {
            if (bro.IsSuccess())
            {
                _inDate = bro.GetInDate();
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
    public void GameDataUpdate(PlayerData data, Action onComplete = null)
    {
        if (data == null || string.IsNullOrEmpty(_inDate))
        {
            Debug.LogError("저장할 데이터가 없습니다");
            onComplete?.Invoke();
            return;
        }

        Param param = PlayerDataToParam(data);
        Backend.GameData.UpdateV2(TABLE_NAME, _inDate, Backend.UserInDate, param, bro =>
        {
            if (bro.IsSuccess())
            {
                Debug.Log("PlayerData 저장 성공");
            }
            else
            {
                Debug.LogError("PlayerData 저장 실패 : " + bro);
            }
            onComplete?.Invoke();
        });
    }

    // -------------------------
    // 파싱 헬퍼
    // -------------------------
    private PlayerData ParsePlayerData(JsonData data)
    {
        return new PlayerData
        {
            PlayerName = data.GetString(COLUMN_PLAYER_NAME, "유저"),
            Gold       = data.GetInt(COLUMN_GOLD, 0),
            Diamond    = data.GetInt(COLUMN_DIAMOND, 0),
            Stamina    = data.GetInt(COLUMN_STAMINA, DefaultMaxStamina),
            MaxStamina = data.GetInt(COLUMN_MAX_STAMINA, DefaultMaxStamina),
            LastStaminaRecoveryTime = data.GetLong(COLUMN_LAST_STAMINA_RECOVERY_TIME, DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            PlayerLevel = data.GetInt(COLUMN_PLAYER_LEVEL, 1),
            PlayerExp   = data.GetInt(COLUMN_PLAYER_EXP, 0),
            MaxExp      = data.GetInt(COLUMN_MAX_EXP, 500),
            LastResetDate = data.GetString(COLUMN_LAST_RESET_DATE, string.Empty)
        };
    }

    private Param PlayerDataToParam(PlayerData data)
    {
        Param param = new Param();
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
        return param;
    }

    // -------------------------
    // 스태미나 관련
    // -------------------------
    public (int stamina, long recoveryTime) CalculateStaminaRecovery(int currentStamina, long lastRecoveryTime, int maxStamina)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long elapsed = now - lastRecoveryTime;
        int recoveryInterval = RecoveryIntervalSeconds;
        int recoveredCount = (int)(elapsed / recoveryInterval);

        if (recoveredCount <= 0) return (currentStamina, lastRecoveryTime);

        int newStamina = Mathf.Min(currentStamina + recoveredCount, maxStamina);
        long newRecoveryTime = lastRecoveryTime + (recoveredCount * recoveryInterval);

        return (newStamina, newRecoveryTime);
    }

    public int GetTimeUntilNextStaminaRecovery(long lastRecoveryTime)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long elapsed = now - lastRecoveryTime;
        int recoveryInterval = RecoveryIntervalSeconds;
        int remaining = recoveryInterval - (int)(elapsed % recoveryInterval);
        return remaining;
    }
}