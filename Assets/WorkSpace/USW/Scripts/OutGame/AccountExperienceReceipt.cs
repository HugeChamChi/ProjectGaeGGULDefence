using System;

/// <summary>Last settled battle. Sequence plus current reservation rejects older replays without an unbounded history.</summary>
[Serializable]
public sealed class AccountExperienceReceipt
{
    /// <summary>Unique reserved battle identifier.</summary>
    public string RunId;
    /// <summary>Owner's monotonically increasing battle number.</summary>
    public long Sequence;
    /// <summary>Actual retained EXP, excluding excess at the cap.</summary>
    public int Experience;
    /// <summary>Account level before settlement.</summary>
    public int LevelBefore;
    /// <summary>Account level after settlement.</summary>
    public int LevelAfter;
}
