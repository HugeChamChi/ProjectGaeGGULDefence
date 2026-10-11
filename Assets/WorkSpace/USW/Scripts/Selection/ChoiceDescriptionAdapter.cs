/// <summary>Captures actual effect values once at presentation, including the existing run-cached random roll.</summary>
public sealed class ChoiceDescriptionAdapter : IDescriptionSource
{
    private readonly LevelUpData _card;
    private readonly LevelUpManager _manager;
    public ChoiceDescriptionAdapter(LevelUpData card, LevelUpManager manager) { _card = card; _manager = manager; }
    public DescriptionSnapshot Capture()
    {
        if (_card == null) return new DescriptionSnapshot(null, null);
        return _manager.GetChoiceDescriptionSnapshot(_card);
    }
}
