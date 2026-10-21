using System;

/// <summary>Pure wall-clock recovery rules, shared by startup, foreground and timer updates.</summary>
public static class StaminaRecovery
{
    /// <summary>Natural recovery never removes purchased over-cap stamina or banks time while full.</summary>
    public static (int Amount, long Timestamp) Calculate(int amount, long timestamp, int maximum, int interval, long now)
    {
        if (maximum <= 0 || interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
        amount = Math.Max(0, amount);
        if (amount >= maximum || timestamp <= 0 || timestamp > now) return (amount, now);
        long ticks = (now - timestamp) / interval;
        if (ticks <= 0) return (amount, timestamp);
        int recovered = (int)Math.Min(ticks, maximum - amount);
        int result = amount + recovered;
        return (result, result >= maximum ? now : timestamp + recovered * (long)interval);
    }

    /// <summary>Returns zero at/above capacity, otherwise seconds to the next recovery.</summary>
    public static int Remaining(int amount, long timestamp, int maximum, int interval, long now)
    {
        if (amount >= maximum) return 0;
        interval = Math.Max(1, interval);
        return interval - (int)(Math.Max(0, now - timestamp) % interval);
    }
}
