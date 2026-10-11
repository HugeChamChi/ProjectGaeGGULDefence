using UnityEngine;

/// <summary>Approved clips and adjustable mix policy; all runtime audio passes through this palette.</summary>
[CreateAssetMenu(menuName = "GaeGGUL/Audio Palette")]
public sealed class GameAudioPalette : ScriptableObject
{
    /// <summary>Continuous background loop shared across app scenes.</summary>
    public AudioClip Music;
    /// <summary>Music's base gain, separate from the player's BGM preference.</summary>
    [Range(0f, 1f)] public float MusicGain = .28f;
    /// <summary>Start/stop transition in real seconds.</summary>
    [Min(.01f)] public float MusicFadeSeconds = 1.2f;
    /// <summary>Real-time duration of a full music playback-speed transition.</summary>
    [Min(.01f)] public float MusicSpeedBlendSeconds = .2f;
    /// <summary>Global one-shot source budget, including important events.</summary>
    [Range(2, 16)] public int VoiceBudget = 8;
    /// <summary>Global repeated one-shot budget, irrespective of emitter count.</summary>
    [Range(1, 4)] public int RepeatedVoiceBudget = 2;
    /// <summary>Conservative sum of SFX gains, leaving headroom for the music.</summary>
    [Range(.1f, .8f)] public float SfxGainCeiling = .7f;
    /// <summary>Repeated sounds lose their sharp high-frequency edge.</summary>
    [Range(1000f, 22000f)] public float RepeatedLowPassHz = 6500f;
    /// <summary>Music gain multiplier while an important cue is audible.</summary>
    [Range(0f, 1f)] public float MusicDuck = .5f;
    /// <summary>Repeated texture gain multiplier while an important cue is audible.</summary>
    [Range(0f, 1f)] public float RepeatedDuck = .2f;
    /// <summary>Fast foreground clearance in real seconds.</summary>
    [Min(.01f)] public float DuckAttackSeconds = .035f;
    /// <summary>Gentle background recovery in real seconds.</summary>
    [Min(.01f)] public float DuckReleaseSeconds = .4f;
    /// <summary>Short one-shot tail fade to avoid hard truncation clicks.</summary>
    [Min(.005f)] public float TailFadeSeconds = .035f;
    /// <summary>Shared continuous laser fades, independent of emitter count.</summary>
    [Min(.01f)] public float LaserFadeSeconds = .08f;
    /// <summary>Seven event definitions; missing or unknown sounds are silent.</summary>
    public AudioCueDefinition[] Cues = new AudioCueDefinition[0];

    /// <summary>Looks up authored event data without loading an arbitrary resource address.</summary>
    public AudioCueDefinition Find(GameAudioCue cue)
    {
        foreach (var entry in Cues) if (entry != null && entry.Cue == cue) return entry;
        return null;
    }

    /// <summary>Allows only exact clip references registered in this palette.</summary>
    public AudioCueDefinition Find(AudioClip clip)
    {
        if (clip == null) return null;
        foreach (var entry in Cues) if (entry != null && entry.Clip == clip) return entry;
        return null;
    }
}
