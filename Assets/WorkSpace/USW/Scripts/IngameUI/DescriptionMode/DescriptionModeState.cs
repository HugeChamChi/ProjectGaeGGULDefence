using System;

/// <summary>Session-owned presentation preference. No Unity, gameplay, or persistence dependency.</summary>
public sealed class DescriptionModeState
{
    /// <summary>New sessions start with brief descriptions.</summary>
    public DescriptionMode Current { get; private set; } = DescriptionMode.Brief;

    /// <summary>Raised once when the preference actually changes.</summary>
    public event Action<DescriptionMode> OnChanged;

    /// <summary>Changes only the presentation preference.</summary>
    public void Set(DescriptionMode mode)
    {
        if (mode != DescriptionMode.Brief && mode != DescriptionMode.Detailed)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (Current == mode) return;
        Current = mode;
        OnChanged?.Invoke(mode);
    }
}
