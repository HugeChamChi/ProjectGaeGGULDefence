/// <summary>In-memory battle ownership, captured before any boss event; invalid after account/session changes.</summary>
public sealed class AccountExperienceTicket
{
    /// <summary>Authenticated PlayerData owner.</summary>
    public readonly string Account;
    /// <summary>Owning controller session generation.</summary>
    public readonly int Generation;
    /// <summary>Unique reserved battle identifier.</summary>
    public readonly string RunId;
    /// <summary>Durable account battle number.</summary>
    public readonly long Sequence;

    /// <summary>Captures a durable battle reservation and its owning data session.</summary>
    public AccountExperienceTicket(string account, int generation, string runId, long sequence)
    { Account = account; Generation = generation; RunId = runId; Sequence = sequence; }
}
