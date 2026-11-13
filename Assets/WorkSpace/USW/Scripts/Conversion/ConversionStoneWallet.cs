using System;

/// <summary>
/// Conversion stones held in the current run. Earned by fast boss kills and spent to convert a placed unit.
/// Scene-scoped: the run ends with the scene and a new game start resets the count.
/// </summary>
public sealed class ConversionStoneWallet
{
    /// <summary>Stones currently held.</summary>
    public int Stones { get; private set; }

    /// <summary>Raised with the new count after every change.</summary>
    public event Action<int> OnChanged;

    /// <summary>Adds earned stones; zero or negative amounts are ignored.</summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;
        Stones += amount;
        OnChanged?.Invoke(Stones);
    }

    /// <summary>Spends stones only when the full amount is available.</summary>
    public bool TrySpend(int amount)
    {
        if (amount < 0 || Stones < amount) return false;
        Stones -= amount;
        OnChanged?.Invoke(Stones);
        return true;
    }

    /// <summary>Clears the count for a new run.</summary>
    public void Reset()
    {
        Stones = 0;
        OnChanged?.Invoke(Stones);
    }
}
