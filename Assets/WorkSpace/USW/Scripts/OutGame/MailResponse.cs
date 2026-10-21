using LitJson;

/// <summary>Transport result independent of Unity SDK callback internals.</summary>
public sealed class MailResponse
{
    public int StatusCode;
    public string Json;
    /// <summary>Only successful transport responses authorize grants.</summary>
    public bool IsSuccess() => StatusCode >= 200 && StatusCode < 300;
    /// <summary>Parses the response payload.</summary>
    public JsonData GetReturnValuetoJSON() => JsonMapper.ToObject(Json);
}
