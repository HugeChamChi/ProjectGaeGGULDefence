/// <summary>Read-only effect explanation; no invented live boss state.</summary>
public sealed class DebuffInfoModel
{
    /// <summary>Effect title.</summary>
    public string Title { get; }
    /// <summary>Explanation and rules resolved from the active definition.</summary>
    public string Body { get; }
    /// <summary>Creates a presentation snapshot.</summary>
    public DebuffInfoModel(string title, string body) { Title = title; Body = body; }
}
