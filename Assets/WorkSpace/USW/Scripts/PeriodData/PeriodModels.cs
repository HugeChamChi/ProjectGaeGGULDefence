using System;
using System.Globalization;
using LitJson;

namespace GGD.Periods
{
    // Add public fields here. Initializers also apply to older saves with missing fields.
    [Serializable]
    public sealed class DailyData
    {
        public int NormalPlayCount = 0;
    }

    [Serializable]
    public sealed class WeeklyData
    {
        public int NormalPlayCount = 0;
    }

    public static class PeriodSerialization
    {
        public static string Write<T>(T data) => JsonMapper.ToJson(data);
        public static T Read<T>(string json) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Period payload cannot be empty.");
            // LitJson constructs T before assigning saved fields, preserving field initializers.
            return JsonMapper.ToObject<T>(json) ?? throw new FormatException("Period payload cannot be null.");
        }

        public static string Key(DateTime utc, bool weekly)
        {
            var day = utc.AddHours(9 - DailyManager.ResetHourKst).Date;
            if (weekly) day = day.AddDays(-((int)day.DayOfWeek + 6) % 7);
            return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
