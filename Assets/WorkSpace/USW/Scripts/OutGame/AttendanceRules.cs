/// <summary>
/// Cumulative 7-day attendance: each new server day allows claiming the next unclaimed day.
/// Missed days do not reset progress; after day 7 the next day starts again at day 1.
/// </summary>
public static class AttendanceRules
{
    /// <summary>Display and claim state for one server day.</summary>
    public readonly struct Status
    {
        /// <summary>Days shown as already received (0-7).</summary>
        public readonly int Claimed;
        /// <summary>Day that can be claimed now (1-7), or 0 when nothing is claimable.</summary>
        public readonly int ClaimableDay;
        /// <summary>Whether the stored claim is already from today.</summary>
        public readonly bool ClaimedToday;

        public Status(int claimed, int claimableDay, bool claimedToday) { Claimed = claimed; ClaimableDay = claimableDay; ClaimedToday = claimedToday; }
        /// <summary>Whether a claim is allowed for today.</summary>
        public bool CanClaim => ClaimableDay > 0;
    }

    /// <summary>Evaluates stored progress against the current server day key (yyyy-MM-dd).</summary>
    public static Status Evaluate(int count, string lastDay, string today)
    {
        int claimed = count < 0 ? 0 : count > AttendanceConfig.CycleDays ? AttendanceConfig.CycleDays : count;
        if (string.IsNullOrEmpty(today)) return new Status(claimed, 0, false);
        lastDay ??= string.Empty;
        if (lastDay == today) return new Status(claimed, 0, true);
        // A stored day later than the server day means the clock moved backwards; never grant in that state.
        if (lastDay.Length > 0 && string.CompareOrdinal(today, lastDay) < 0) return new Status(claimed, 0, false);
        if (claimed >= AttendanceConfig.CycleDays) claimed = 0;
        return new Status(claimed, claimed + 1, false);
    }
}
