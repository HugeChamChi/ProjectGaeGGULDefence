using LitJson;

public static class BackendUtils
{
    public static string GetString(this JsonData data, string key, string fallback = "")
        => data.Keys.Contains(key) ? data[key]?.ToString() ?? fallback : fallback;

    public static int GetInt(this JsonData data, string key, int fallback = 0)
        => data.Keys.Contains(key) && int.TryParse(data[key]?.ToString(), out int val) ? val : fallback;

    public static long GetLong(this JsonData data, string key, long fallback = 0)
        => data.Keys.Contains(key) && long.TryParse(data[key]?.ToString(), out long val) ? val : fallback;
}