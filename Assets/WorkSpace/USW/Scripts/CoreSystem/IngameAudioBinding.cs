using System;
using VContainer.Unity;

/// <summary>Scene-owned combat audibility; app audio and preferences retain their root lifetime.</summary>
public sealed class IngameAudioBinding : ITickable, IDisposable
{
    private readonly AudioManager _audio;
    private readonly GameManager _game;
    private readonly TimeScaleService _time;
    private readonly FieldPauseVisuals _field;
    private bool _audible;

    /// <summary>Receives scene state and the root audio through VContainer.</summary>
    public IngameAudioBinding(AudioManager audio, GameManager game, TimeScaleService time, FieldPauseVisuals field)
    { _audio = audio; _game = game; _time = time; _field = field; _audio.SetCombatActive(false); }

    /// <inheritdoc />
    public void Tick()
    {
        _audio.SetMusicSpeed(_game.CurrentState == GameManager.GameState.Playing ? _time.Current : 1f);
        _audio.SetCombatSpeed(_time.Current);
        bool audible = _game.CurrentState == GameManager.GameState.Playing && !_time.IsPaused && !_field.AttacksHeld;
        if (_audible == audible) return;
        _audible = audible;
        _audio.SetCombatActive(audible);
    }

    /// <summary>Releases scene emitters; persistent music continues across navigation.</summary>
    public void Dispose()
    {
        if (_audio == null) return;
        _audio.ClearCombatAudio();
        _audio.SetMusicSpeed(1f);
    }
}
