using Spine.Unity;
using UnityEngine;

/// <summary>Isolated visual experiment: command, teleport, single SkyLaser pulse, return.</summary>
public sealed class GammanBlinkFxLab : MonoBehaviour, IFxLabPlayable
{
    [SerializeField] private GammanBlinkStyle[] _styles;
    [SerializeField] private SkyLaserFx[] _shots;
    [SerializeField] private SpriteRenderer _drone;
    [SerializeField] private SpriteRenderer[] _ghosts;
    [SerializeField] private Renderer[] _rings;
    [SerializeField] private SkeletonAnimation _gamman;
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _home = new Vector3(-.537f, -2.02f, -.4f);
    [SerializeField] private float _rest = 1f;
    [SerializeField] private bool _autoCompare = true;
    [SerializeField] private GammanHackDetonationFx _detonation;
    private int _index;
    private int _phase;
    private float _elapsed;
    private bool _running;
    private MaterialPropertyBlock _block;
    private static readonly string[] Phases = { "READY", "COMMAND", "DEPART", "BLINK", "ARRIVE", "ONE SHOT", "RETURN", "HOME" };
    private GammanBlinkStyle Style => _styles[_index];
    private Vector3 Air => _target.position + Style.AirOffset;

    /// <inheritdoc/>
    public bool UseUnscaledTime { get; set; }
    /// <summary>Current comparison index.</summary>
    public int CurrentStyle => _index;
    /// <summary>Current visual stage for capture and verification.</summary>
    public string CurrentPhase => Phases[_phase];

    private void Start() => Play();
    private void OnDisable() => Stop();

    /// <inheritdoc/>
    public void Play() => Select(_index);

    /// <summary>Replay an authored variant, clearing the previous shot and teleport visuals.</summary>
    public void Select(int index)
    {
        if (_styles == null || _styles.Length == 0) return;
        Stop();
        _index = Mathf.Clamp(index, 0, _styles.Length - 1);
        _phase = 0;
        _elapsed = 0f;
        _running = true;
        _gamman.Initialize(false);
        _gamman.AnimationState.SetAnimation(0, "idle", true);
        RenderPhase();
    }

    /// <summary>Stop all presentation and put the drone back at its origin.</summary>
    public void Stop()
    {
        _running = false;
        if (_detonation != null) _detonation.Clear();
        if (_shots != null) foreach (var shot in _shots) if (shot != null) shot.Stop();
        if (_rings != null) foreach (var ring in _rings) if (ring != null) ring.enabled = false;
        if (_ghosts != null) foreach (var ghost in _ghosts) if (ghost != null) ghost.enabled = false;
        if (_drone != null) { _drone.enabled = true; _drone.color = Color.white; _drone.transform.position = _home; _drone.transform.rotation = Quaternion.identity; }
    }

    private float Duration => _phase == 0 || _phase == 7 ? _rest :
        _phase == 1 ? Style.CommandTime : _phase == 3 ? Style.TransitTime : Style.BlinkTime;

    private void Update()
    {
        if (!_running) return;
        _elapsed += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if ((_phase == 5 && !_shots[_index].IsPlaying && (_detonation == null || _elapsed >= Style.BurstDelay + Style.BurstDuration)) || (_phase != 5 && _elapsed >= Duration))
        {
            _elapsed = 0f;
            _phase++;
            if (_phase > 7) { Select(_autoCompare ? (_index + 1) % _styles.Length : _index); return; }
            if (_phase == 1)
            {
                _gamman.AnimationState.SetAnimation(0, "skill", false);
                _gamman.AnimationState.AddAnimation(0, "idle", true, 0f);
            }
            if (_phase == 5) { _shots[_index].UseUnscaledTime = UseUnscaledTime; _shots[_index].Play(); }
        }
        RenderPhase();
    }

    private void RenderPhase()
    {
        float p = Mathf.Clamp01(_elapsed / Mathf.Max(.01f, Duration));
        bool air = _phase >= 4 && _phase <= 6;
        _drone.transform.position = air ? Air : _home;
        _drone.transform.rotation = air ? Quaternion.Euler(0, 0, Mathf.Atan2(-Style.AirOffset.y, -Style.AirOffset.x) * Mathf.Rad2Deg - 180f) : Quaternion.identity;
        _drone.enabled = _phase != 3 && (_phase != 5 || !_shots[_index].IsPlaying);
        float alpha = _phase == 2 || _phase == 6 ? 1f - p : _phase == 4 ? p : 1f;
        _drone.color = new Color(1, 1, 1, alpha);
        _drone.transform.localScale = Vector3.one * (1.6f * (Style.Portal ? Mathf.Lerp(.25f, 1f, alpha) : 1f));
        _drone.transform.position -= _drone.transform.rotation * Vector3.Scale(_drone.sprite.bounds.center, _drone.transform.localScale);
        _block ??= new MaterialPropertyBlock();
        for (int i = 0; i < _rings.Length; i++)
        {
            bool visible = _phase == 2 || _phase == 4 || _phase == 6;
            var ring = _rings[i];
            ring.enabled = visible;
            Vector3 pos = i == 0 ? _home : Air;
            ring.transform.position = pos + new Vector3(0, 0, -.08f);
            float scale = Style.RingSize * Mathf.Lerp(.25f, 1.4f, p);
            ring.transform.localScale = new Vector3(scale, scale * (Style.Portal ? .45f : 1f), 1);
            var tint = Style.Tint;
            tint.a *= Mathf.Sin(p * Mathf.PI);
            _block.SetColor("_Color", tint);
            ring.SetPropertyBlock(_block);
        }
        for (int i = 0; i < _ghosts.Length; i++)
        {
            var ghost = _ghosts[i];
            ghost.enabled = Style.Afterimages && (_phase == 2 || _phase == 4 || _phase == 6);
            ghost.transform.SetPositionAndRotation(_drone.transform.position + Vector3.up * ((i + 1) * .16f * p), _drone.transform.rotation);
            ghost.color = new Color(Style.Tint.r, Style.Tint.g, Style.Tint.b, (1f - p) * .3f / (i + 1));
        }
        if (_detonation != null) _detonation.Render(Style, _phase, _elapsed);
    }

    private void OnGUI()
    {
        float scale = Mathf.Max(.6f, Screen.width / 540f);
        var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale;
        if (_styles == null || _styles.Length == 0) { GUI.matrix = old; return; }
        GUI.Box(new Rect(10, 10, width - 20, 176), _detonation != null ? "GAMMAN / HACK DETONATION" : "GAMMAN / BLINK STRIKE");
        GUI.Label(new Rect(24, 37, width - 48, 24), Style.Label + "  /  " + CurrentPhase);
        GUI.Label(new Rect(24, 61, width - 48, 24), Style.Description);
        float buttonWidth = (width - 56f) / 3f;
        for (int i = 0; i < _styles.Length; i++)
            if (GUI.Button(new Rect(24 + (i % 3) * (buttonWidth + 4), 94 + (i / 3) * 40, buttonWidth, 34), _styles[i].Label)) { _autoCompare = false; Select(i); }
        if (GUI.Button(new Rect(24 + 2 * (buttonWidth + 4), 134, buttonWidth, 34), _autoCompare ? "AUTO: ON" : "AUTO: OFF")) _autoCompare = !_autoCompare;
        GUI.matrix = old;
    }
}
