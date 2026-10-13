using TMPro;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Standalone glitch-link audition using the same bounded palette and mixer as combat.</summary>
public sealed class DisigmanGlitchLinkAudio : MonoBehaviour
{
    [SerializeField] private GameAudioPalette _palette;
    [SerializeField] private AudioMixer _mixer;
    [SerializeField] private TMP_Text _toggleLabel;
    private UnityAudioProvider _provider;
    private bool _soundEnabled = true;

    /// <summary>Whether this audition currently permits audio.</summary>
    public bool SoundEnabled => _soundEnabled;

    private void Awake()
    {
        if (_palette == null || _mixer == null) return;
        _provider = new UnityAudioProvider(gameObject, _mixer, _palette);
        _provider.Init();
        foreach (AudioGroup group in System.Enum.GetValues(typeof(AudioGroup)))
        {
            _provider.SetVolume(group, PlayerPrefs.GetInt("Audio_Vol_" + group, 100));
            _provider.SetMute(group, PlayerPrefs.GetInt("Audio_Mute_" + group, 0) == 1);
        }
        _provider.SetCombatActive(true);
    }

    /// <summary>Starts a cancellable charge on the preview effect's scaled clock.</summary>
    public void Charge(Object owner, bool skill)
    {
        if (!_soundEnabled) return;
        _provider?.SetCombatSpeed(Time.timeScale);
        _provider?.PlayCue(_palette?.Find(skill ? GameAudioCue.DisigmanSkillCharge : GameAudioCue.DisigmanBasicCharge), 0f, owner);
    }

    /// <summary>Ends the charge and plays the shot at the visual impact boundary.</summary>
    public void Fire(Object owner, bool skill)
    {
        _provider?.StopOwnedCue(owner);
        if (_soundEnabled) _provider?.PlayCue(_palette?.Find(skill ? GameAudioCue.DisigmanSkillShot : GameAudioCue.DisigmanBasicShot), 0f);
    }

    /// <summary>Clears pending charges and tails when changing a visual variant.</summary>
    public void CancelAll()
    {
        _provider?.ClearCombat();
        _provider?.SetCombatActive(_soundEnabled && isActiveAndEnabled);
    }

    /// <summary>Toggles only audition sound, preserving the player's volume preferences.</summary>
    public void ToggleSound()
    {
        _soundEnabled = !_soundEnabled;
        CancelAll();
        if (_toggleLabel != null) _toggleLabel.text = _soundEnabled ? "소리 켜짐" : "소리 꺼짐";
    }

    private void Update()
    {
        _provider?.SetCombatSpeed(Time.timeScale);
        _provider?.Tick(Time.unscaledDeltaTime);
    }

    private void OnDisable() => _provider?.ClearCombat();
    private void OnDestroy() => _provider?.Dispose();
}
