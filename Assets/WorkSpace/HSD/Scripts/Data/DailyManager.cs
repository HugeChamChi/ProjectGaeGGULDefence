using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using GaeGGUL.Core;

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
        DateTime nowUtc = Server.GetServerTime();
        DateTime nowKst = Server.ToKST(nowUtc);
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
}
