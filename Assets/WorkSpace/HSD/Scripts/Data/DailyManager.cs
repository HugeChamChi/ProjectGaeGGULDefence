using System;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 뒤끝 서버 시간을 기준으로 매일 초기화 로직을 담당하는 클래스
/// </summary>
public class DailyManager
{
    public event Action OnDailyReset;
    public bool IsNewDay { get; private set; }

    public async UniTask InitializeAsync()
    {
        DateTime serverTime = GetServerTime();
        string currentDate = serverTime.ToString("yyyy-MM-dd");

        string lastResetDate = Player.PlayerData.Data.LastResetDate;

        if (lastResetDate != currentDate)
        {
            Debug.Log($"[DailyManager] 새로운 하루 시작! (이전: {lastResetDate}, 현재: {currentDate})");
            IsNewDay = true;

            ResetDailyData();
            Player.PlayerData.Data.LastResetDate = currentDate;
        }
        else
        {
            IsNewDay = false;
            Debug.Log($"[DailyManager] 이미 오늘 초기화가 완료되었습니다. ({currentDate})");
        }
    }

    private void ResetDailyData()
    {
        OnDailyReset?.Invoke();
    }

    public DateTime GetServerTime()
    {
        var bro = Backend.Utils.GetServerTime();
        if (bro.IsSuccess())
        {
            string timeStr = bro.GetReturnValuetoJSON()["utcTime"].ToString();
            return DateTime.Parse(timeStr).ToUniversalTime();
        }

        Debug.LogWarning("[DailyManager] 서버 시간을 불러오지 못했습니다. 로컬 시간을 사용합니다.");
        return DateTime.UtcNow;
    }
}
