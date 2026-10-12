using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Player-controlled audio mixer channels; serialized values retain their existing order.</summary>
public enum AudioGroup { BGM, SFX, Master }

/// <summary>App-owned audio entry point. Only sounds authored in the approved palette can play.</summary>
public class AudioManager : MonoBehaviour
{
    private const string MixerResourcePath = "Master";
    private const string VolumeKeyPrefix = "Audio_Vol_";
    private const string MuteKeyPrefix = "Audio_Mute_";
    [SerializeField] private GameAudioPalette _palette;
    private IAudioProvider _provider;
    private UnityAudioProvider _unityProvider;
    private readonly Dictionary<string, AudioCueDefinition> _cueByName = new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);

    /// <summary>Existing 0–100 master preference.</summary>
    public int MasterVolume { get => GetVolume(AudioGroup.Master); set => SetVolume(AudioGroup.Master, value); }
    /// <summary>Existing 0–100 music preference, independent of authored mix gains.</summary>
    public int BGMVolume { get => GetVolume(AudioGroup.BGM); set => SetVolume(AudioGroup.BGM, value); }
    /// <summary>Existing 0–100 effects preference, independent of authored mix gains.</summary>
    public int SFXVolume { get => GetVolume(AudioGroup.SFX); set => SetVolume(AudioGroup.SFX, value); }

    protected void Awake()
    {
        var mixer = RM.Load<AudioMixer>(MixerResourcePath);
        if (mixer == null || _palette == null)
        {
            Debug.LogError("[AudioManager] Master mixer and approved GameAudioPalette are required.");
            return;
        }
        _unityProvider = new UnityAudioProvider(gameObject, mixer, _palette);
        foreach (var cue in _palette.Cues)
        {
            if (cue == null) continue;
            _cueByName[cue.Cue.ToString()] = cue;
            if (cue.Clip != null) _cueByName[cue.Clip.name] = cue;
        }
        _provider = _unityProvider;
        _provider.Init();
    }

    private void Start()
    {
        // Mixer parameters are applied after Awake, preserving all existing preference keys.
        LoadSettings();
    }
    private void Update() => _unityProvider?.Tick(Time.unscaledDeltaTime);
    private void OnDestroy() => _unityProvider?.Dispose();

    /// <summary>Applies and persists the existing volume preference.</summary>
    public void SetVolume(AudioGroup group, int volume) { _provider?.SetVolume(group, volume); SaveSettings(); }
    /// <summary>Reads the existing volume preference.</summary>
    public int GetVolume(AudioGroup group) => _provider?.GetVolume(group) ?? 0;
    /// <summary>Applies and persists the existing mute preference.</summary>
    public void SetMute(AudioGroup group, bool isMute) { _provider?.SetMute(group, isMute); SaveSettings(); }
    /// <summary>Reads the existing mute preference.</summary>
    public bool IsMuted(AudioGroup group) => _provider?.IsMuted(group) ?? false;

    private void SaveSettings()
    {
        foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
        {
            PlayerPrefs.SetInt(VolumeKeyPrefix + group, GetVolume(group));
            PlayerPrefs.SetInt(MuteKeyPrefix + group, IsMuted(group) ? 1 : 0);
        }
        PlayerPrefs.Save();
    }
    private void LoadSettings()
    {
        foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
        {
            _provider?.SetVolume(group, PlayerPrefs.GetInt(VolumeKeyPrefix + group, 100));
            _provider?.SetMute(group, PlayerPrefs.GetInt(MuteKeyPrefix + group, 0) == 1);
        }
    }

    /// <summary>Starts the authored in-game music when its owning scene enters.</summary>
    public void PlayIngameBGM()
    {
        if (_palette != null) PlayBGM(_palette.Music);
    }

    /// <summary>Legacy string entry accepts only the approved music's name/path.</summary>
    public void PlayBGM(string address)
    {
        if (_palette?.Music != null && ClipName(address) == _palette.Music.name) PlayBGM(_palette.Music);
    }
    /// <summary>Loops the approved music; repeated requests never restart the loop.</summary>
    public void PlayBGM(AudioClip clip)
    { if (_palette != null && clip == _palette.Music) _provider?.PlayBGM(clip); }
    /// <summary>Fades out music without overwriting the user's volume preference.</summary>
    public void StopBGM() => _provider?.StopBGM();

    /// <summary>Blends BGM speed with gameplay slowdown; zero keeps music audible at normal speed.</summary>
    public void SetMusicSpeed(float gameplaySpeed) => _unityProvider?.SetMusicSpeed(gameplaySpeed);

    /// <summary>Plays the authored cue under its global cooldown, voice cap and priority.</summary>
    public void PlayCue(GameAudioCue cue) => _unityProvider?.PlayCue(_palette?.Find(cue));
    /// <summary>Starts an authored charge owned by a projectile so interruption can cancel it.</summary>
    public void PlayOwnedCue(GameAudioCue cue, UnityEngine.Object owner) => _unityProvider?.PlayCue(_palette?.Find(cue), 0f, owner);
    /// <summary>Cancels only the given owner's charge, preserving already-fired shot tails.</summary>
    public void StopOwnedCue(UnityEngine.Object owner) => _unityProvider?.StopOwnedCue(owner);
    /// <summary>Updates the authored combat cues' playback clock.</summary>
    public void SetCombatSpeed(float speed) => _unityProvider?.SetCombatSpeed(speed);
    /// <summary>Shares one sustained laser texture between all live emitters.</summary>
    public void SetLaserActive(UnityEngine.Object owner, bool active) => _unityProvider?.SetLaserActive(owner, active);
    /// <summary>Scene binding permits combat textures only while the battlefield is running.</summary>
    public void SetCombatActive(bool active) => _unityProvider?.SetCombatActive(active);
    /// <summary>Scene exit clears outstanding laser emitters while app music continues.</summary>
    public void ClearCombatAudio() => _unityProvider?.ClearCombat();

    /// <summary>Compatibility entry for serialized events. Unknown and retired sounds remain silent.</summary>
    public void PlaySFX(string address, float pitchRandomness = .1f)
    {
        var cue = ResolveCue(address);
        if (cue != null) _unityProvider?.PlayCue(cue, pitchRandomness);
    }
    /// <summary>Raw clips are accepted only when they are exact approved palette references.</summary>
    public void PlaySFX(AudioClip clip, float pitchRandomness = .1f)
    { if (_palette?.Find(clip) != null) _provider?.PlaySFX(clip, pitchRandomness); }

    private AudioCueDefinition ResolveCue(string address)
    {
        if (string.IsNullOrEmpty(address) || _palette == null) return null;
        string name = ClipName(address);
        if (_cueByName.TryGetValue(name, out var authored)) return authored;
        // Only semantically compatible legacy calls survive the transition. No arbitrary RM.Load.
        switch (name)
        {
            case "02.Summon":
                return _palette.Find(GameAudioCue.UnitSummon);
            case "05.Drone_Attack":
                return _palette.Find(GameAudioCue.DroneLaser);
            default: return null;
        }
    }

    private static string ClipName(string address)
    {
        if (string.IsNullOrEmpty(address)) return string.Empty;
        string name = address.Replace('\\', '/');
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name.Substring(slash + 1);
        return name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
    }

    /// <summary>Prepares only palette clips. Caller cancellation never unloads app-owned audio.</summary>
    public async UniTask PreloadPaletteAsync(CancellationToken cancellationToken)
    {
        if (_palette == null) return;
        foreach (var cue in _palette.Cues)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cue?.Clip == null) continue;
            if (cue.Clip.loadState == AudioDataLoadState.Unloaded) cue.Clip.LoadAudioData();
            await UniTask.WaitWhile(() => cue.Clip.loadState == AudioDataLoadState.Loading, cancellationToken: cancellationToken);
            if (cue.Clip.loadState == AudioDataLoadState.Failed)
                Debug.LogError("[AudioManager] Approved clip failed to load: " + cue.Clip.name);
        }
    }

    /// <summary>Compatibility preload never loads retired addresses; it prepares the app palette.</summary>
    public UniTask PreloadSFXAsync(IEnumerable<string> addresses)
        => PreloadPaletteAsync(this.GetCancellationTokenOnDestroy());
    /// <summary>The root palette owns clip references until app teardown; scene callers cannot unload them.</summary>
    public void ReleasePreloadedSFX() { }
}
