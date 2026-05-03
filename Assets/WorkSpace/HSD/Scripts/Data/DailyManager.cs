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
    const int RESET_HOUR = 5;

    public async UniTask InitializeAsync()
    {
        // 한국 표준시(KST, UTC+9) 기준으로 초기화 시간을 설정합니다.
        // 현재 KST 시간에서 RESET_HOUR(5시)를 차감했을 때의 날짜를 '현재 정산일'로 간주합니다.
        // 예: 새벽 4:59(KST) -> 5시간 차감 시 전날 23:59 -> 전날 날짜로 인식
        //     새벽 5:00(KST) -> 5시간 차감 시 오늘 00:00 -> 오늘 날짜로 인식
        DateTime nowUtc = GetServerTime();
        DateTime nowKst = nowUtc.AddHours(9);
        string currentDate = nowKst.AddHours(-RESET_HOUR).ToString("yyyy-MM-dd");

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
