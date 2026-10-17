using UnityEngine;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Preserves the existing device-local preview key and JSON format.</summary>
    public sealed class DeckUpgradePreviewStore : IDeckUpgradeStore
    {
        /// <inheritdoc />
        public DeckUpgradeProgress Load() => PlayerPrefs.HasKey(DeckUpgradeProgress.SaveKey)
            ? JsonUtility.FromJson<DeckUpgradeProgress>(PlayerPrefs.GetString(DeckUpgradeProgress.SaveKey))
            : new DeckUpgradeProgress();

        /// <inheritdoc />
        public void Save(DeckUpgradeProgress progress)
        {
            PlayerPrefs.SetString(DeckUpgradeProgress.SaveKey, JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
        }
    }
}
