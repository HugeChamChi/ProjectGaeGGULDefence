/// <summary>Injectable random stream; values must be finite and in [0, 1).</summary>
public interface IEndlessRandom
{
    /// <summary>Returns the next uniform draw.</summary>
    double NextUnit();
}
