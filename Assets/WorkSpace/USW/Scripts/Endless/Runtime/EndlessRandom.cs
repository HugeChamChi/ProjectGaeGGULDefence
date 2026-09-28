/// <summary>Scene-owned random stream independent of combat critical rolls.</summary>
public sealed class EndlessRandom : IEndlessRandom
{
    private readonly System.Random _random = new System.Random();
    /// <inheritdoc />
    public double NextUnit() => _random.NextDouble();
}
