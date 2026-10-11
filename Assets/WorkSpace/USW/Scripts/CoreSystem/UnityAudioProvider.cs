using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Bounded, priority-aware Unity playback for the approved sound palette.</summary>
public sealed class UnityAudioProvider : IAudioProvider, IDisposable
{
    private sealed class Voice
    {
        internal AudioSource Source;
        internal AudioLowPassFilter Filter;
        internal AudioCueDefinition Cue;
        internal float EndsAt;
    }

    private readonly AudioMixer _mainMixer;
    private readonly GameObject _owner;
    private readonly GameAudioPalette _palette;
    private readonly Dictionary<AudioGroup, AudioGroupData> _groups = new();
    private readonly Dictionary<GameAudioCue, float> _lastPlayedTime = new();
    private readonly HashSet<UnityEngine.Object> _laserOwners = new();
    private readonly System.Random _presentationRandom = new System.Random();
    private readonly List<Voice> _voices = new();
    private static readonly Predicate<UnityEngine.Object> DestroyedOwner = IsDestroyed;
    private AudioSource _music, _laser;
    private float _musicEnvelope, _laserEnvelope, _duck;
    private float _musicSpeed = 1f;
    private bool _musicRequested, _combatActive;

    /// <summary>Uses the app-owned host, existing user-volume mixer and authored palette.</summary>
    public UnityAudioProvider(GameObject owner, AudioMixer mixer, GameAudioPalette palette)
    { _owner = owner; _mainMixer = mixer; _palette = palette; }

    /// <inheritdoc />
    public void Init()
    {
        if (_mainMixer == null || _palette == null) return;
        foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
        {
            var matches = _mainMixer.FindMatchingGroups(group.ToString());
            var data = new AudioGroupData(group, group + "_Vol", matches.Length > 0 ? matches[0] : null);
            _groups.Add(group, data);
        }
        _music = CreateSource("Palette_BGM", AudioGroup.BGM);
        _music.loop = true;
        _groups[AudioGroup.BGM].Source = _music;
        _laser = CreateSource("Palette_SharedLaser", AudioGroup.SFX);
        _laser.loop = true;
        _laser.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = _palette.RepeatedLowPassHz;
        for (int i = 0; i < _palette.VoiceBudget; i++)
        {
            var source = CreateSource("Palette_SFX_" + i, AudioGroup.SFX);
            var filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            filter.enabled = false;
            _voices.Add(new Voice { Source = source, Filter = filter });
        }
        _groups[AudioGroup.SFX].Source = _voices[0].Source;
    }

    private AudioSource CreateSource(string name, AudioGroup group)
    {
        var child = new GameObject(name);
        child.transform.SetParent(_owner.transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = _groups[group].MixerGroup;
        return source;
    }

    /// <inheritdoc />
    public void PlayBGM(AudioClip clip)
    {
        if (_music == null || clip == null || clip != _palette.Music) return;
        _musicRequested = true;
        if (_music.isPlaying && _music.clip == clip) return;
        _music.clip = clip;
        _music.volume = 0f;
        _musicEnvelope = 0f;
        _music.Play();
    }

    /// <inheritdoc />
    public void StopBGM() => _musicRequested = false;

    /// <summary>Follows positive gameplay slowdown; a full pause keeps music running normally.</summary>
    public void SetMusicSpeed(float gameplaySpeed)
        => _musicSpeed = gameplaySpeed > 0f ? Mathf.Clamp01(gameplaySpeed) : 1f;

    /// <inheritdoc />
    public void PlaySFX(AudioClip clip, float pitchRandomness = .1f)
    {
        var cue = _palette?.Find(clip);
        if (cue != null) PlayCue(cue, pitchRandomness);
    }

    /// <summary>Admits only an authored one-shot and never steals a more important voice.</summary>
    public void PlayCue(AudioCueDefinition cue, float pitchRandomness = .1f)
    {
        if (cue == null || cue.Clip == null || _palette.Find(cue.Cue) != cue
            || cue.Cue == GameAudioCue.SustainedLaser || (cue.Repeated && !_combatActive)) return;
        float now = Time.unscaledTime;
        if (_lastPlayedTime.TryGetValue(cue.Cue, out float last) && now - last < cue.MinimumInterval) return;
        int same = 0, repeated = 0;
        Voice available = null, weakest = null, oldestSame = null;
        foreach (var voice in _voices)
        {
            if (voice.Cue != null && (!voice.Source.isPlaying || now >= voice.EndsAt)) Stop(voice);
            if (voice.Cue == null) { if (available == null) available = voice; continue; }
            if (voice.Cue.Cue == cue.Cue)
            {
                same++;
                if (oldestSame == null || voice.EndsAt < oldestSame.EndsAt) oldestSame = voice;
            }
            if (voice.Cue.Repeated) repeated++;
            if (weakest == null || voice.Cue.Priority < weakest.Cue.Priority) weakest = voice;
        }
        if (same >= cue.MaximumVoices)
        {
            // Each accepted UI press gets feedback even while earlier click tails are playing.
            // Reuse a HUD voice without raising the authored overlap or combat voice budgets.
            if (cue.Cue != GameAudioCue.HudButton) return;
            available = oldestSame;
            Stop(available);
        }
        if (cue.Repeated && repeated >= _palette.RepeatedVoiceBudget) return;
        if (available == null)
        {
            if (weakest == null || weakest.Cue.Priority >= cue.Priority) return;
            available = weakest;
            Stop(available);
        }
        _lastPlayedTime[cue.Cue] = now;
        available.Cue = cue;
        available.Filter.enabled = cue.Repeated;
        available.Filter.cutoffFrequency = _palette.RepeatedLowPassHz;
        available.Source.clip = cue.Clip;
        available.Source.priority = 255 - cue.Priority;
        float variation = Mathf.Min(cue.PitchVariation, Mathf.Max(0f, pitchRandomness));
        available.Source.pitch = 1f + (float)(_presentationRandom.NextDouble() * 2d - 1d) * variation;
        float duration = cue.Clip.length / available.Source.pitch;
        if (cue.MaximumSeconds > 0f) duration = Mathf.Min(duration, cue.MaximumSeconds);
        available.EndsAt = now + duration;
        available.Source.volume = 0f;
        available.Source.Play();
        UpdateGains(now);
    }

    /// <summary>Many simultaneous laser emitters share one continuous texture.</summary>
    public void SetLaserActive(UnityEngine.Object owner, bool active)
    {
        if (ReferenceEquals(owner, null)) return;
        if (active && owner != null) _laserOwners.Add(owner); else _laserOwners.Remove(owner);
    }

    /// <summary>Suspends repetitive combat audio while keeping UI, music and settlement audible.</summary>
    public void SetCombatActive(bool active)
    {
        _combatActive = active;
        if (!active) foreach (var voice in _voices) if (voice.Cue?.Repeated == true) Stop(voice);
    }

    /// <summary>Clears emitter ownership on scene exit.</summary>
    public void ClearCombat()
    {
        SetCombatActive(false); _laserOwners.Clear();
        foreach (var voice in _voices)
            if (voice.Cue != null && voice.Cue.Cue != GameAudioCue.HudButton)
                voice.EndsAt = Mathf.Min(voice.EndsAt, Time.unscaledTime + _palette.TailFadeSeconds);
    }

    /// <summary>Advances fades on real time; no async job can outlive the app audio host.</summary>
    public void Tick(float deltaTime)
    {
        if (_music == null) return;
        float now = Time.unscaledTime;
        bool important = false;
        _music.pitch = Mathf.MoveTowards(_music.pitch, _musicSpeed,
            deltaTime / Mathf.Max(.01f, _palette.MusicSpeedBlendSeconds));
        foreach (var voice in _voices)
        {
            if (voice.Cue == null) continue;
            if (!voice.Source.isPlaying || now >= voice.EndsAt) { Stop(voice); continue; }
            important |= voice.Cue.DucksBackground;
        }
        if (IsMuted(AudioGroup.SFX) || GetVolume(AudioGroup.SFX) == 0) important = false;
        float duckSeconds = important ? _palette.DuckAttackSeconds : _palette.DuckReleaseSeconds;
        _duck = Mathf.MoveTowards(_duck, important ? 1f : 0f, deltaTime / duckSeconds);
        _musicEnvelope = Mathf.MoveTowards(_musicEnvelope, _musicRequested ? 1f : 0f, deltaTime / _palette.MusicFadeSeconds);
        if (!_musicRequested && _musicEnvelope <= 0f) _music.Stop();
        _laserOwners.RemoveWhere(DestroyedOwner);
        bool laserWanted = _combatActive && _laserOwners.Count > 0;
        var loop = _palette.Find(GameAudioCue.SustainedLaser);
        if (laserWanted && loop?.Clip != null && !_laser.isPlaying)
        {
            _laser.clip = loop.Clip;
            _laser.Play();
        }
        _laserEnvelope = Mathf.MoveTowards(_laserEnvelope, laserWanted ? 1f : 0f, deltaTime / _palette.LaserFadeSeconds);
        if (!laserWanted && _laserEnvelope <= 0f) _laser.Stop();
        UpdateGains(now);
    }

    private static bool IsDestroyed(UnityEngine.Object owner) => owner == null;
    private float VoiceGain(Voice voice, float now)
    {
        float tail = Mathf.Clamp01((voice.EndsAt - now) / _palette.TailFadeSeconds);
        float duck = voice.Cue.Repeated ? Mathf.Lerp(1f, _palette.RepeatedDuck, _duck) : 1f;
        return voice.Cue.Gain * tail * duck;
    }

    private void UpdateGains(float now)
    {
        float loopGain = (_palette.Find(GameAudioCue.SustainedLaser)?.Gain ?? 0f) * _laserEnvelope
            * Mathf.Lerp(1f, _palette.RepeatedDuck, _duck);
        float sum = loopGain;
        foreach (var voice in _voices) if (voice.Cue != null) sum += VoiceGain(voice, now);
        float headroom = sum > _palette.SfxGainCeiling ? _palette.SfxGainCeiling / sum : 1f;
        foreach (var voice in _voices) if (voice.Cue != null) voice.Source.volume = VoiceGain(voice, now) * headroom;
        _laser.volume = loopGain * headroom;
        _music.volume = _palette.MusicGain * _musicEnvelope * Mathf.Lerp(1f, _palette.MusicDuck, _duck);
    }

    private static void Stop(Voice voice)
    {
        if (voice.Source != null) { voice.Source.Stop(); voice.Source.clip = null; }
        voice.Cue = null;
    }

    /// <inheritdoc />
    public void SetVolume(AudioGroup group, int volume)
    {
        if (!_groups.TryGetValue(group, out var data)) return;
        data.Volume = volume; data.Apply(_mainMixer);
    }
    /// <inheritdoc />
    public int GetVolume(AudioGroup group) => _groups.TryGetValue(group, out var data) ? data.Volume : 0;
    /// <inheritdoc />
    public void SetMute(AudioGroup group, bool isMute)
    {
        if (!_groups.TryGetValue(group, out var data)) return;
        data.IsMuted = isMute; data.Apply(_mainMixer);
    }
    /// <inheritdoc />
    public bool IsMuted(AudioGroup group) => _groups.TryGetValue(group, out var data) && data.IsMuted;
    /// <summary>Stops all playback and releases runtime references with the app lifetime.</summary>
    public void Dispose()
    {
        foreach (var voice in _voices) Stop(voice);
        if (_music != null) _music.Stop();
        if (_laser != null) _laser.Stop();
        _laserOwners.Clear(); _lastPlayedTime.Clear();
    }
}
