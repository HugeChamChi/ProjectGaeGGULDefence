namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Typed character-upgrade state shared by the list and detail views.</summary>
    public sealed class DeckUpgradeItemViewData
    {
        /// <summary>Index in the authored unit list; -1 represents the chief.</summary>
        public int Index { get; internal set; }
        /// <summary>Authored display name, separate from the level label.</summary>
        public string Name { get; internal set; }
        /// <summary>Current preview level.</summary>
        public int Level { get; internal set; }
        /// <summary>Current preview balance.</summary>
        public int Gold { get; internal set; }
        /// <summary>Numeric cost of the next level.</summary>
        public int Cost { get; internal set; }
        /// <summary>Whether the preview level limit has been reached.</summary>
        public bool IsMaxLevel => Level >= DeckUpgradeProgress.MaxLevel;
        /// <summary>Whether the next upgrade is currently affordable.</summary>
        public bool CanUpgrade => !IsMaxLevel && Gold >= Cost;
        /// <summary>Whether this entry is the chief.</summary>
        public bool IsChief => Index < 0;
        /// <summary>Current chief-wide preview bonus.</summary>
        public float ChiefBonus { get; internal set; }
        /// <summary>Authored skill name.</summary>
        public string SkillName { get; internal set; }
        /// <summary>Authored skill description, without deriving it from other UI text.</summary>
        public string Description { get; internal set; }
        /// <summary>Formatted current-to-next main stat for the original detail layout.</summary>
        public string StatComparison { get; internal set; }
        /// <summary>Formatted current-to-next speed stat for the original detail layout.</summary>
        public string SpeedComparison { get; internal set; }
        /// <summary>Formatted current main stat for the stats tab.</summary>
        public string CurrentStat { get; internal set; }
        /// <summary>Formatted current speed stat for the stats tab.</summary>
        public string CurrentSpeed { get; internal set; }
    }
}
