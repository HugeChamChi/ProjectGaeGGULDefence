/// <summary>Encounter-side cell contract. Tier zero is empty; one is Normal.</summary>
public sealed class DeertreeCellState
{
    public int Tier { get; internal set; }
    public bool HasRoot { get; internal set; }
    /// <summary>Host-owned: the cell cannot take a root although it holds no unit (totem, permanent seal).</summary>
    public bool Blocked { get; internal set; }
    public int RootBindTapsLeft { get; internal set; }
    public int CurseTapsLeft { get; internal set; }
    public float CurseRemaining { get; internal set; }
    /// <summary>A bound unit cannot act until released or timed out.</summary>
    public bool CanAct => Tier > 0 && CurseTapsLeft == 0 && RootBindTapsLeft == 0;
}
