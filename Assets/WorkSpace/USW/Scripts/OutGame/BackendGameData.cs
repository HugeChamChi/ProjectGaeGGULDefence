using System;
using UnityEngine;
using BackEnd;
using LitJson;

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
    }

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
            PlayerName = data["PlayerName"]?.ToString() ?? "유저",
            Gold = int.TryParse(data["Gold"]?.ToString(), out int gold) ? gold : 0,
            Diamond = int.TryParse(data["Diamond"]?.ToString(), out int diamond) ? diamond : 0,
            Stamina = int.TryParse(data["Stamina"]?.ToString(), out int stamina) ? stamina : 30,
            MaxStamina = int.TryParse(data["MaxStamina"]?.ToString(), out int maxStamina) ? maxStamina : 30,
            LastStaminaRecoveryTime = long.TryParse(data["LastStaminaRecoveryTime"]?.ToString(), out long recoveryTime) ? recoveryTime : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            PlayerLevel = int.TryParse(data["PlayerLevel"]?.ToString(), out int level) ? level : 1,
            PlayerExp = int.TryParse(data["PlayerExp"]?.ToString(), out int exp) ? exp : 0,
            MaxExp = int.TryParse(data["MaxExp"]?.ToString(), out int maxExp) ? maxExp : 500
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