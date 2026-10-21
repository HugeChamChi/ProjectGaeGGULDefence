/// <summary>Explicit selection policy; old authored profiles retain fixed-cycle behavior.</summary>
public enum EndlessBossSelection
{
    FixedCycle = 0,
    ShuffleAfterOpening = 1,
    /// <summary>Use the role-specific shuffle bags starting with round one.</summary>
    ShuffleFromStart = 2
}
