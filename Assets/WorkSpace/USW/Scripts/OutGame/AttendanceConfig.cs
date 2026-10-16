using System;
using UnityEngine;

/// <summary>Authored 7-day attendance rewards and the server-day boundary. Amounts are trial values until approved.</summary>
[CreateAssetMenu(menuName = "Game/Attendance Config", fileName = "AttendanceConfig")]
public sealed class AttendanceConfig : ScriptableObject
{
    /// <summary>Number of days in one cycle; the UI lays out six regular days and one featured final day.</summary>
    public const int CycleDays = 7;
    /// <summary>Popup title.</summary>
    public string Title = "7일 출석 보상";
    /// <summary>Footer shown under the reward grid.</summary>
    [TextArea] public string Footer = "출석 보상은 수령 즉시 지급됩니다. 7일차를 받으면 1일차부터 다시 시작합니다.";
    /// <summary>Offset from UTC used to name a server day (KST = 9).</summary>
    public int UtcOffsetHours = 9;
    /// <summary>Local hour at which a new attendance day begins; matches DailyManager's 05:00 reset.</summary>
    [Range(0, 23)] public int ResetHour = 5;
    /// <summary>Exactly seven rewards, day 1 first.</summary>
    public Reward[] Rewards = new Reward[CycleDays];

    /// <summary>One day's grant.</summary>
    [Serializable]
    public sealed class Reward
    {
        public MailRewardConfig.Currency Currency;
        [Min(1)] public int Amount = 1;
    }

    /// <summary>True when every day has a positive supported reward.</summary>
    public bool IsValid
    {
        get
        {
            if (Rewards == null || Rewards.Length != CycleDays) return false;
            foreach (var reward in Rewards) if (reward == null || reward.Amount <= 0 || !Enum.IsDefined(typeof(MailRewardConfig.Currency), reward.Currency)) return false;
            return true;
        }
    }

    /// <summary>Server-day key such as 2026-10-17 for the given UTC instant.</summary>
    public string DayKey(DateTime utc) => utc.ToUniversalTime().AddHours(UtcOffsetHours - ResetHour).ToString("yyyy-MM-dd");

    /// <summary>UTC instant at which the day containing <paramref name="utc"/> ends.</summary>
    public DateTime NextReset(DateTime utc)
    {
        DateTime shifted = utc.ToUniversalTime().AddHours(UtcOffsetHours - ResetHour);
        return DateTime.SpecifyKind(shifted.Date.AddDays(1).AddHours(ResetHour - UtcOffsetHours), DateTimeKind.Utc);
    }
}
