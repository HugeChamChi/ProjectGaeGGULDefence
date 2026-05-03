using System;
using UnityEngine;
using BackEnd;
using LitJson;
using Cysharp.Threading.Tasks;

public class BackendGameData : MonoBehaviour
{
    public static BackendGameData Instance { get; private set; }

    private string _inDate;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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
        var bro = Backend.GameData.GetMyData("PlayerData", new Where());

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
        }
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
            Stamina = 30,
            MaxStamina = 30,
            LastStaminaRecoveryTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            PlayerLevel = 1,
            PlayerExp = 0,
            MaxExp = 500
        };

        Param param = PlayerDataToParam(newData);
        var bro = Backend.GameData.Insert("PlayerData", param);

        if (bro.IsSuccess())
        {
            _inDate = bro.GetInDate();
            Debug.Log("PlayerData 생성 성공");
            onComplete?.Invoke(newData);
        }
        else
        {
            Debug.LogError("PlayerData 생성 실패 : " + bro);
        }
    }

    // -------------------------
    // 데이터 저장
    // -------------------------
    public void GameDataUpdate(PlayerData data, Action onComplete = null)
    {
        if (data == null || string.IsNullOrEmpty(_inDate))
        {
            Debug.LogError("저장할 데이터가 없습니다");
            return;
        }

        Param param = PlayerDataToParam(data);
        var bro = Backend.GameData.UpdateV2("PlayerData", _inDate, Backend.UserInDate, param);

        if (bro.IsSuccess())
        {
            Debug.Log("PlayerData 저장 성공");
            onComplete?.Invoke();
        }
        else
        {
            Debug.LogError("PlayerData 저장 실패 : " + bro);
        }
    }

    // -------------------------
    // 파싱 헬퍼
    // -------------------------
    private PlayerData ParsePlayerData(JsonData data)
    {
        return new PlayerData
        {
            PlayerName = data.GetString("PlayerName", "유저"),
            Gold       = data.GetInt("Gold", 0),
            Diamond    = data.GetInt("Diamond", 0),
            Stamina    = data.GetInt("Stamina", 30),
            MaxStamina = data.GetInt("MaxStamina", 30),
            LastStaminaRecoveryTime = data.GetLong("LastStaminaRecoveryTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            PlayerLevel = data.GetInt("PlayerLevel", 1),
            PlayerExp   = data.GetInt("PlayerExp", 0),
            MaxExp      = data.GetInt("MaxExp", 500),
            LastResetDate = data.GetString("LastResetDate", string.Empty)
        };
    }

    private Param PlayerDataToParam(PlayerData data)
    {
        Param param = new Param();
        param.Add("PlayerName", data.PlayerName);
        param.Add("Gold", data.Gold);
        param.Add("Diamond", data.Diamond);
        param.Add("Stamina", data.Stamina);
        param.Add("MaxStamina", data.MaxStamina);
        param.Add("LastStaminaRecoveryTime", data.LastStaminaRecoveryTime);
        param.Add("PlayerLevel", data.PlayerLevel);
        param.Add("PlayerExp", data.PlayerExp);
        param.Add("MaxExp", data.MaxExp);
        param.Add("LastResetDate", data.LastResetDate);
        return param;
    }

    // -------------------------
    // 스태미나 관련
    // -------------------------
    public (int stamina, long recoveryTime) CalculateStaminaRecovery(int currentStamina, long lastRecoveryTime, int maxStamina)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long elapsed = now - lastRecoveryTime;
        int recoveryInterval = 300;
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
        int recoveryInterval = 300;
        int remaining = recoveryInterval - (int)(elapsed % recoveryInterval);
        return remaining;
    }
}