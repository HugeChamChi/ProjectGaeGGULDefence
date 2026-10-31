using System;

namespace HSD.UI.Setting
{
    /// <summary>Presentation contract preserved by both the serialized host and authored settings renderer.</summary>
    public interface ISettingsView
    {
        /// <summary>Silently displays the service's current sound settings.</summary>
        void UpdateSoundSlot(AudioGroup group, int volume, bool isMuted);
        /// <summary>Silently displays presentation preferences.</summary>
        void UpdatePresentationToggles(bool damageNumbers, bool vibration);
        /// <summary>Uses the existing native confirmation and navigation flow.</summary>
        void ShowConfirm(string title, string description, Action onYes, Action onNo = null);
    }
}
