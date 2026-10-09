using UnityEngine;

/// <summary>Compare one, three or five independent Gamman actors with simultaneous or staggered casting.</summary>
public sealed class GammanTeleportGroupLab : MonoBehaviour, IFxLabPlayable
{
    [SerializeField] private GammanTeleportFxLab[] _members;
    [SerializeField] private GammanTeleportTake[] _takes;
    [SerializeField] private int _count = 3;
    [SerializeField] private bool _stagger;
    [SerializeField] private float _staggerSeconds = .08f;
    [SerializeField] private bool _auto = true;
    [SerializeField] private float _crowdMotionScale = .65f;
    private int _selected;
    private bool _running;

    /// <inheritdoc/>
    public bool UseUnscaledTime { get; set; }
    /// <summary>Visible actor count.</summary>
    public int ActorCount => _count;
    /// <summary>Whether comparison-only phase offsets are enabled.</summary>
    public bool Staggered => _stagger;
    /// <summary>Selected choreography.</summary>
    public int CurrentTake => _selected;
    private void Start() => Play();
    private void OnDisable()
    {
        _running = false;
        if (_members != null) foreach (var member in _members) if (member != null) member.Stop();
    }
    /// <inheritdoc/>
    public void Play() => Select(_selected);
    /// <summary>Restart the complete group; late actors finish before automatic comparison advances.</summary>
    public void Select(int index)
    {
        _selected = Mathf.Clamp(index, 0, _takes.Length - 1);
        for (int i = 0; i < _members.Length; i++)
        {
            var member = _members[i];
            bool active = i < _count;
            member.transform.parent.gameObject.SetActive(active);
            if (!active) continue;
            member.UseUnscaledTime = UseUnscaledTime;
            member.AirMotionScale = _count == 1 ? 1f : _crowdMotionScale;
            member.BeginTake(_selected, _stagger ? i * _staggerSeconds : 0);
        }
        _running = true;
    }
    /// <summary>Configure crowd size and timing for a lab comparison; never changes production cooldowns.</summary>
    public void Configure(int count, bool stagger)
    {
        _count = count <= 1 ? 1 : count <= 3 ? 3 : 5;
        _stagger = stagger; Play();
    }
    private void Update()
    {
        if (!_running) return;
        for (int i = 0; i < _count; i++) if (!_members[i].Finished) return;
        Select(_auto ? (_selected + 1) % _takes.Length : _selected);
    }
    private void OnGUI()
    {
        float s = Mathf.Max(.6f, Screen.width / 540f); var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float w = Screen.width / s, bw = (w - 56) / 3;
        GUI.Box(new Rect(10, 10, w - 20, 225), "GAMMAN / TELEPORT GROUP COMPARISON");
        GUI.Label(new Rect(24, 37, w - 48, 24), _takes[_selected].Label + " / " + _count + " ACTORS / " + (_stagger ? "STAGGERED" : "SIMULTANEOUS"));
        GUI.Label(new Rect(24, 61, w - 48, 24), _takes[_selected].Description);
        for (int i = 0; i < _takes.Length; i++)
            if (GUI.Button(new Rect(24 + i % 3 * (bw + 4), 94 + i / 3 * 40, bw, 34), _takes[i].Label)) { _auto = false; Select(i); }
        if (GUI.Button(new Rect(24 + 2 * (bw + 4), 134, bw, 34), _auto ? "AUTO: ON" : "AUTO: OFF")) _auto = !_auto;
        if (GUI.Button(new Rect(24, 183, 65, 34), "1")) Configure(1, _stagger);
        if (GUI.Button(new Rect(94, 183, 65, 34), "3")) Configure(3, _stagger);
        if (GUI.Button(new Rect(164, 183, 65, 34), "5")) Configure(5, _stagger);
        if (GUI.Button(new Rect(238, 183, w - 262, 34), _stagger ? "TIMING: STAGGER" : "TIMING: SAME")) Configure(_count, !_stagger);
        GUI.matrix = old;
    }
}
