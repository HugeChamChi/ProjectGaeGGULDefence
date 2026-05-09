using LitJson;

public static class BackendUtils
{
    /// <summary>
    /// JsonData에서 안전하게 문자열을 추출합니다. 
    /// {"S" : "value"} 또는 {"N" : "123"} 형태의 중첩 구조도 자동으로 처리합니다.
    /// </summary>
    public static string GetSafeString(this JsonData data, string key, string fallback = "")
    {
        if (data == null || !data.Keys.Contains(key)) return fallback;
        var value = data[key];
        if (value == null) return fallback;

        // 뒤끝 특유의 {"S":"abc"} 또는 {"N":"123"} 구조 대응
        if (value.IsObject)
        {
            if (value.Keys.Contains("S")) return value["S"].ToString();
            if (value.Keys.Contains("N")) return value["N"].ToString();
        }

        return value.ToString();
    }

    public static int GetSafeInt(this JsonData data, string key, int fallback = 0)
    {
        string strValue = GetSafeString(data, key, null);
        return (strValue != null && int.TryParse(strValue, out int result)) ? result : fallback;
    }

    public static long GetSafeLong(this JsonData data, string key, long fallback = 0)
    {
        string strValue = GetSafeString(data, key, null);
        return (strValue != null && long.TryParse(strValue, out long result)) ? result : fallback;
    }

    public static double GetSafeDouble(this JsonData data, string key, double fallback = 0)
    {
        string strValue = GetSafeString(data, key, null);
        return (strValue != null && double.TryParse(strValue, out double result)) ? result : fallback;
    }

    // 기존 메서드 유지 (하위 호환성)
    public static string GetString(this JsonData data, string key, string fallback = "") => GetSafeString(data, key, fallback);
    public static int GetInt(this JsonData data, string key, int fallback = 0) => GetSafeInt(data, key, fallback);
    public static long GetLong(this JsonData data, string key, long fallback = 0) => GetSafeLong(data, key, fallback);
}