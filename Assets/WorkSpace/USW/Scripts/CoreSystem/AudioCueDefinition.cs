using System;
using UnityEngine;

/// <summary>Authored playback and information priority for one approved sound.</summary>
[Serializable]
public sealed class AudioCueDefinition
{
    /// <summary>Semantic event, independent of source filenames.</summary>
    public GameAudioCue Cue;
    /// <summary>The sole approved source for this event.</summary>
    public AudioClip Clip;
    /// <summary>Linear gain before user volume and mix headroom.</summary>
    [Range(0f, 1f)] public float Gain = .5f;
    /// <summary>Higher values may replace lower priority voices.</summary>
    [Range(0, 255)] public int Priority = 60;
    /// <summary>Minimum unscaled interval across every emitter of this event.</summary>
    [Min(0f)] public float MinimumInterval = .1f;
    /// <summary>Maximum simultaneous voices of this event across the battlefield.</summary>
    [Min(1)] public int MaximumVoices = 1;
    /// <summary>Zero plays the complete one-shot; positive values fade its tail early.</summary>
    [Min(0f)] public float MaximumSeconds;
    /// <summary>Small presentation-only pitch variation; never consumes gameplay RNG.</summary>
    [Range(0f, .1f)] public float PitchVariation;
    /// <summary>Repeated combat texture is quiet, filtered and paused with combat.</summary>
    public bool Repeated;
    /// <summary>Important information ducks music and repeated combat textures.</summary>
    public bool DucksBackground;
}
