using System;

/// <summary>Checked conversion from run multipliers to the existing float combat pipeline.</summary>
public static class RunStatMath
{
    /// <summary>Scales attack-based damage before existing rounding. Fixed damage bypasses this.</summary>
    public static float ScaleAttack(float damage, double multiplier)
    {
        if (!(multiplier > 0d) || double.IsInfinity(multiplier)) throw new OverflowException("Invalid attack multiplier.");
        float result = ToFloat(damage * multiplier, damage > 0f, "attack damage");
        if ((double)result > int.MaxValue) throw new OverflowException("Run attack damage exceeds the existing Int32 damage pipeline.");
        return result;
    }

    /// <summary>Divides the existing interval by frequency, preserving the original base calculation.</summary>
    public static float ScaleAttackInterval(float interval, double frequency)
    {
        if (!(frequency > 0d) || double.IsInfinity(frequency)) throw new OverflowException("Invalid attack frequency.");
        float result = ToFloat(interval / frequency, interval > 0f, "attack interval");
        ToDelayMilliseconds(result);
        return result;
    }

    /// <summary>Detects the existing UniTask Int32 millisecond delay limit before any narrowing cast.</summary>
    public static int ToDelayMilliseconds(float seconds)
    {
        // Preserve DroneUnit's float multiplication + RoundToInt semantics.
        float milliseconds = seconds * 1000f;
        if (float.IsNaN(milliseconds) || milliseconds < 0f || (double)milliseconds > int.MaxValue)
            throw new OverflowException("Attack interval exceeds the existing Int32 millisecond delay range.");
        return (int)Math.Round(milliseconds);
    }

    private static float ToFloat(double value, bool positiveInput, string label)
    {
        float result = (float)value;
        if (double.IsNaN(value) || double.IsInfinity(value) || float.IsInfinity(result)
            || (positiveInput && !(result > 0f)))
            throw new OverflowException($"Run {label} exceeds float precision/range; long-run numeric policy required.");
        return result;
    }
}
