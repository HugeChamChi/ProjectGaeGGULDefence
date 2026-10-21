using System;

/// <summary>Account-scoped evidence, written before submitting an irreversible claim.</summary>
[Serializable]
public sealed class MailClaimJournal
{
    public string Id;
    public string Title;
    public string Response;
}
