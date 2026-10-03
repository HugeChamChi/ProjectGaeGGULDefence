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
    [SerializeField] private string _attackStart = "attack_start";
    [SerializeField] private string _attackLoop = "attack_loop";
    [SerializeField] private string _attackEnd = "attack_end";
    private MeshRenderer _renderer;
    private bool _dragging;
    private bool _paused;
    private bool _previousUnscaled;

    private void Awake()
    {
        if (_skeleton != null) _renderer = _skeleton.GetComponent<MeshRenderer>();
    }

    private void OnEnable() => ResetAnimation();
    private void OnDisable() { SetPauseIdle(false); ResetAnimation(); }

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
    }

    private bool Has(string name) => !string.IsNullOrEmpty(name)
        && _skeleton.Skeleton.Data.FindAnimation(name) != null;

    /// <summary>Plays one authored firing cycle and returns to idle; never applies combat effects.</summary>
    public void PlayAttack()
    {
        if (_skeleton == null || _skeleton.AnimationState == null || !Has(_attackStart)) return;
        var state = _skeleton.AnimationState;
        state.SetAnimation(0, _attackStart, false);
        if (Has(_attackLoop)) state.AddAnimation(0, _attackLoop, false, 0f);
        if (Has(_attackEnd)) state.AddAnimation(0, _attackEnd, false, 0f);
        if (Has(_idle)) state.AddAnimation(0, _idle, true, 0f);
    }

    /// <inheritdoc />
    public void SetPauseIdle(bool on)
    {
        if (_skeleton == null || on == _paused) return;
        _paused = on;
        if (on) { _previousUnscaled = _skeleton.UnscaledTime; _skeleton.UnscaledTime = true; }
        else _skeleton.UnscaledTime = _previousUnscaled;
    }

    private void LateUpdate()
    {
        if (_skeleton == null || _source == null || _renderer == null) return;
        _renderer.sortingLayerID = _source.sortingLayerID;
        _renderer.sortingOrder = _source.sortingOrder;
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
