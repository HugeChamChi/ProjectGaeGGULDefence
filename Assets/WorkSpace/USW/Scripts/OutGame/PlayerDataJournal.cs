using System;

/// <summary>Account-keyed durable failed-save snapshot and the last confirmed server version it extends.</summary>
[Serializable]
public sealed class PlayerDataJournal
{
    /// <summary>Confirmed server snapshot on which this local edit was based.</summary>
    public string BaseSaveId;
    /// <summary>Latest local absolute values; retries do not add the amounts again.</summary>
    public PlayerData Data;
}
