namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Persistence boundary for the isolated character-upgrade preview.</summary>
    public interface IDeckUpgradeStore
    {
        /// <summary>Loads the preview's progress without accessing account data.</summary>
        DeckUpgradeProgress Load();

        /// <summary>Persists the preview's progress.</summary>
        void Save(DeckUpgradeProgress progress);
    }
}
