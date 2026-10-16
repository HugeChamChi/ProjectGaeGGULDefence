using System;

/// <summary>Pure server-time reminder calculation; never grants rewards or changes recovery state.</summary>
public static class LocalNotificationPlanner
{
    private const int KstOffsetHours = 9;

    /// <summary>UTC instant when natural recovery reaches capacity, or null if already full/invalid.</summary>
    public static DateTime? StaminaFull(PlayerData data, int intervalSeconds, DateTime serverUtc)
    {
        if (data == null || data.MaxStamina <= 0 || intervalSeconds <= 0) return null;
        long now = new DateTimeOffset(serverUtc).ToUnixTimeSeconds();
        var recovered = StaminaRecovery.Calculate(data.Stamina, data.LastStaminaRecoveryTime, data.MaxStamina, intervalSeconds, now);
        if (recovered.Amount >= data.MaxStamina) return null;
        long full = recovered.Timestamp + (long)(data.MaxStamina - recovered.Amount) * intervalSeconds;
        return DateTimeOffset.FromUnixTimeSeconds(full).UtcDateTime;
    }

    /// <summary>Next KST reminder whose attendance day is unclaimed; repeat daily from this instant.</summary>
    public static DateTime? NextAttendance(PlayerData data, AttendanceConfig attendance, LocalNotificationConfig config, DateTime serverUtc)
    {
        if (data == null || attendance == null || config == null || !attendance.IsValid) return null;
        // A future stored day is an unresolved clock/data conflict, not permission to notify.
        if (string.CompareOrdinal(data.AttendanceLastDay ?? "", attendance.DayKey(serverUtc)) > 0) return null;
        DateTime candidate = DateTime.SpecifyKind(serverUtc.AddHours(KstOffsetHours).Date
            .AddHours(config.AttendanceHourKst).AddMinutes(config.AttendanceMinute).AddHours(-KstOffsetHours), DateTimeKind.Utc);
        if (candidate <= serverUtc) candidate = candidate.AddDays(1);
        if (!AttendanceRules.Evaluate(data.AttendanceCount, data.AttendanceLastDay, attendance.DayKey(candidate)).CanClaim)
            candidate = candidate.AddDays(1);
        return candidate;
    }
}
