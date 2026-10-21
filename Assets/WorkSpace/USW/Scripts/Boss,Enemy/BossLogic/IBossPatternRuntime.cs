/// <summary>Optional boss-owned effects advanced exclusively by the scene pattern controller's combat clock.</summary>
public interface IBossPatternRuntime
{
    /// <summary>Connects scene-owned placement services when this boss's patterns are registered.</summary>
    void ConfigurePatternContext(GridManager grid);
    /// <summary>Advances cast effects and lingering effects while combat is running.</summary>
    void AdvancePatternEffects(float deltaTime);
    /// <summary>Releases all temporary effects on removal, death, cancellation or battle end.</summary>
    void ClearPatternEffects();
}
