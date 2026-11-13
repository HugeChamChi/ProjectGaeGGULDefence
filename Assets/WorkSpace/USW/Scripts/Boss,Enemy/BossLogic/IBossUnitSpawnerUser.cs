/// <summary>Optional boss contract for patterns that demote or remove placed units through the scene spawner.</summary>
public interface IBossUnitSpawnerUser
{
    /// <summary>Called before <see cref="IBossPatternRuntime.ConfigurePatternContext"/>. Null in scenes without a spawner.</summary>
    void ConfigureUnitSpawner(UnitSpawner spawner);
}
