using Spine.Unity;
using UnityEngine;

/// <summary>Spine appearance with the existing sprite's sorting/tint contract and pooled animation reset.</summary>
[DefaultExecutionOrder(200)]
public sealed class SpineActorVisual : MonoBehaviour, IPauseIdleVisual
{
    [SerializeField] private SkeletonAnimation _skeleton;
    [SerializeField] private SpriteRenderer _source;
    [SerializeField] private DragHandler _drag;
    [SerializeField] private string _idle = "idle";
    [SerializeField] private string _dragAnimation = "drag";
    [SerializeField] private string _attack = "attack";
    [SerializeField] private string _skill = "skill";
    [SerializeField] private string _attackStart = "attack_start";
    [SerializeField] private string _attackLoop = "attack_loop";
    [SerializeField] private string _attackEnd = "attack_end";
    [Tooltip("Seconds into the authored attack/attack_start where playback begins. Lines up the recoil frame with a projectile that charges before firing. 0 = from the start.")]
    [SerializeField, Min(0f)] private float _attackStartTime;
    [Tooltip("Seconds into the authored skill where playback begins. 0 = from the start.")]
    [SerializeField, Min(0f)] private float _skillStartTime;
    [Tooltip("Loop the attack continuously with one full cycle per attack interval (speed = attack length / interval), so the unit never returns to idle. On each attack the phase is re-aligned so the recoil frame lands Shot Lead seconds after the attack. For units whose interval differs from the motion length.")]
    [SerializeField] private bool _syncAttackToInterval;
    [Tooltip("Sync mode: seconds into the authored attack where the shot/recoil happens.")]
    [SerializeField, Min(0f)] private float _attackShotTime;
    [Tooltip("Sync mode: real seconds between the attack call and the visible shot (e.g. a railgun charge).")]
    [SerializeField, Min(0f)] private float _shotLeadSeconds;
    [Tooltip("Breaks draw-order ties with neighbours (same sorting order) by grid position: back rows behind, and in a row the neighbour this unit faces is drawn in front, so a long weapon goes behind that neighbour instead of flickering.")]
    [SerializeField] private bool _depthByPosition;
    private const float RowDepth = 0.01f, ColumnDepth = 0.001f;
    private Spine.TrackEntry _loopEntry;
    private float _nextAttackAt;
    private MeshRenderer _renderer;
    private UnitBase _unit;
    private SpineAttackSpeedSync _attackSpeed;
    private bool _dragging;
    private bool _paused;
    private bool _previousUnscaled;

    /// <summary>The authored skeleton used for visual-only skill copies.</summary>
    public SkeletonAnimation Skeleton => _skeleton;

    /// <summary>Animated muzzle position, including the authored skeleton scale and pose.</summary>
    public Vector3 MuzzlePosition
    {
        get
        {
            var bone = _skeleton != null ? _skeleton.Skeleton?.FindBone("muzzle") : null;
            return bone != null ? bone.GetWorldPosition(_skeleton.transform) : transform.position;
        }
    }

    private void Awake()
    {
        if (_skeleton != null) _renderer = _skeleton.GetComponent<MeshRenderer>();
        _unit = GetComponent<UnitBase>();
        if (_skeleton != null)
        {
            _attackSpeed = gameObject.AddComponent<SpineAttackSpeedSync>();
            _attackSpeed.Initialize(_skeleton, _unit, GetComponent<DroneUnit>(), _drag, _idle);
            _attackSpeed.RegisterAttackCycle(_attack);
            _attackSpeed.RegisterAttackCycle(_attackStart, _attackLoop, _attackEnd);
        }
    }

    private void OnEnable()
    {
        ResetAnimation();
        if (_unit == null) return;
        _unit.onAttack?.AddListener(PlayAttack);
        _unit.onSkillFull?.AddListener(PlaySkill);
    }

    private void OnDisable()
    {
        if (_unit != null)
        {
            _unit.onAttack?.RemoveListener(PlayAttack);
            _unit.onSkillFull?.RemoveListener(PlaySkill);
        }
        SetPauseIdle(false);
        ResetAnimation();
    }

    /// <summary>Resets tracks on pool reuse without changing the authored skin.</summary>
    public void ResetAnimation()
    {
        _dragging = false;
        if (_skeleton == null) return;
        _skeleton.Initialize(false);
        var state = _skeleton.AnimationState;
        if (state == null) return;
        state.ClearTracks();
        _skeleton.Skeleton.SetupPose();
        if (Has(_idle)) state.SetAnimation(0, _idle, true);
        _attackSpeed?.Refresh();
    }

    private bool Has(string name) => !string.IsNullOrEmpty(name)
        && _skeleton.Skeleton.Data.FindAnimation(name) != null;

    /// <summary>Plays one authored firing cycle and returns to idle; never applies combat effects.</summary>
    public void PlayAttack()
    {
        if (_skeleton == null || _skeleton.AnimationState == null || (_drag != null && _drag.IsDragging)) return;
        var state = _skeleton.AnimationState;
        var current = state.GetTrack(0);
        if (current != null && current.Animation.Name == _skill && !current.IsComplete) return;
        if (Has(_attack))
        {
            if (_syncAttackToInterval)
            {
                float interval = Mathf.Max(0.05f, _unit != null ? _unit.GetCurrentAttackInterval() : 1f);
                _nextAttackAt = Time.time + interval;
                StartAttackLoop(current, 0f);
                return;
            }
            RestartAttack(state, _attack);
            QueueIdle();
            _attackSpeed?.Refresh();
            return;
        }
        if (!Has(_attackStart)) return;
        RestartAttack(state, _attackStart);
        if (Has(_attackLoop)) state.AddAnimation(0, _attackLoop, false, 0f);
        if (Has(_attackEnd)) state.AddAnimation(0, _attackEnd, false, 0f);
        QueueIdle();
        _attackSpeed?.Refresh();
    }

    private void RestartAttack(Spine.AnimationState state, string animation)
    {
        var current = state.GetTrack(0);
        // Repeated attacks can arrive before a mix finishes. Spine retains the
        // interrupted mixing chain, which otherwise grows on every fast shot.
        // Retire only an already interrupted basic attack; skills retain their
        // authored tracks/events and an ordinary transition still blends normally.
        if (current?.MixingFrom != null && IsAttack(current.Animation.Name)) state.ClearTrack(0);
        var entry = state.SetAnimation(0, animation, false);
        if (_attackStartTime > 0f) entry.TrackTime = Mathf.Min(_attackStartTime, entry.Animation.Duration);
    }

    private bool IsAttack(string animation) => animation == _attack || animation == _attackStart
        || animation == _attackLoop || animation == _attackEnd;

    /// <summary>Plays the authored skill once, then returns to idle without changing skill effects or timing.</summary>
    public void PlaySkill()
    {
        if (_skeleton == null || _skeleton.AnimationState == null || !Has(_skill)) return;
        var entry = _skeleton.AnimationState.SetAnimation(0, _skill, false);
        if (_skillStartTime > 0f) entry.TrackTime = Mathf.Min(_skillStartTime, entry.Animation.Duration);
        SetAttackLoop(null);
        if (!_syncAttackToInterval) QueueIdle();
    }

    private void SetAttackLoop(Spine.TrackEntry entry)
    {
        _loopEntry = entry;
        if (_attackSpeed != null) _attackSpeed.ExternallyTimed = entry;
    }

    /// <summary>
    /// Sync mode: plays (or keeps) the looping attack at one cycle per interval, phased so the shot frame is reached
    /// <paramref name="secondsUntilAttack"/> + Shot Lead seconds from now.
    /// </summary>
    private void StartAttackLoop(Spine.TrackEntry current, float secondsUntilAttack)
    {
        var state = _skeleton.AnimationState;
        float interval = Mathf.Max(0.05f, _unit != null ? _unit.GetCurrentAttackInterval() : 1f);
        var animation = _skeleton.Skeleton.Data.FindAnimation(_attack);
        float duration = animation.Duration;
        float rate = Mathf.Clamp(duration / interval, 0.1f, SpineAttackSpeedSync.MaximumAttackRate);
        float phase = Mathf.Repeat(_attackShotTime - (_shotLeadSeconds + secondsUntilAttack) * rate, duration);
        var entry = current != null && current == _loopEntry ? current : null;
        if (entry == null)
        {
            entry = state.SetAnimation(0, _attack, true);
            entry.TrackTime = phase;
        }
        else
        {
            // Already looping: nudge only real drift (attack-speed change, timer jitter) so the loop never jumps visibly.
            float now = Mathf.Repeat(entry.TrackTime, duration);
            float drift = Mathf.DeltaAngle(now / duration * 360f, phase / duration * 360f) / 360f * duration;
            if (Mathf.Abs(drift) > 0.03f) entry.TrackTime += drift;
        }
        entry.TimeScale = rate;
        SetAttackLoop(entry);
    }

    /// <summary>Sync mode: when a skill ends, resume the attack loop in phase with the next attack.</summary>
    private void Update()
    {
        if (!_syncAttackToInterval || _skeleton == null || _paused || _dragging || !Has(_attack)) return;
        var current = _skeleton.AnimationState?.GetTrack(0);
        if (current == null || current == _loopEntry || current.Next != null) return;
        // Only after a finished skill; idle before the first attack or after a drag waits for the next real attack.
        if (current.Animation.Name != _skill || current.TrackTime < current.AnimationEnd) return;
        StartAttackLoop(null, Mathf.Max(0f, _nextAttackAt - Time.time));
    }

    private void QueueIdle()
    {
        if (Has(_idle)) _skeleton.AnimationState.AddAnimation(0, _idle, true, 0f);
    }

    /// <inheritdoc />
    public void SetPauseIdle(bool on)
    {
        if (_skeleton == null || on == _paused) return;
        _paused = on;
        _attackSpeed?.SetPauseIdle(on);
        _attackSpeed?.Refresh();
        if (on) { _previousUnscaled = _skeleton.UnscaledTime; _skeleton.UnscaledTime = true; }
        else _skeleton.UnscaledTime = _previousUnscaled;
    }

    private void LateUpdate()
    {
        if (_skeleton == null || _source == null || _renderer == null) return;
        _renderer.sortingLayerID = _source.sortingLayerID;
        _renderer.sortingOrder = _source.sortingOrder;
        if (_depthByPosition)
        {
            // Root scale.x > 0 = facing left (UnitBase.ApplyFacingByCell). Larger z draws behind (orthographic, default sort).
            var p = transform.position;
            float facing = transform.lossyScale.x >= 0f ? 1f : -1f;
            var local = _skeleton.transform.localPosition;
            float z = (p.y * RowDepth + facing * p.x * ColumnDepth) / Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.z));
            if (!Mathf.Approximately(local.z, z)) _skeleton.transform.localPosition = new Vector3(local.x, local.y, z);
        }
        var color = _source.color;
        var skeleton = _skeleton.Skeleton;
        skeleton.SetColor(color.r, color.g, color.b, color.a);
        bool dragging = _drag != null && _drag.IsDragging;
        if (dragging == _dragging) return;
        _dragging = dragging;
        string animation = dragging && Has(_dragAnimation) ? _dragAnimation : _idle;
        if (Has(animation)) _skeleton.AnimationState.SetAnimation(0, animation, true);
    }
}
