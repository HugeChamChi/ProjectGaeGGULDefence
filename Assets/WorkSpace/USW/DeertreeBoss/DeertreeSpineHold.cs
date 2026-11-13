using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>Owns an effect pose on the combat clock and prevents its authored exit from playing until removed.</summary>
public sealed class DeertreeSpineHold : MonoBehaviour
{
    private SkeletonAnimation _animation;
    private SkeletonRenderer _renderer;
    private TrackEntry _track;
    private float _holdAt, _age;
    /// <summary>The sampled native animation time, including indefinite holds.</summary>
    public float PoseTime => _track?.TrackTime ?? 0;

    /// <summary>Starts the authored entrance and holds its intact pose until Clear is called.</summary>
    public void Initialize(SkeletonAnimation animation, float holdAt)
    {
        _animation = animation; _renderer = animation.GetComponent<SkeletonRenderer>();
        _animation.enabled = false; _holdAt = holdAt; _age = 0;
        _animation.AnimationState.ClearTracks(); _animation.Skeleton.SetupPose();
        _track = _animation.AnimationState.SetAnimation(0, "animation", false); _track.MixDuration = 0;
        Advance(0);
    }

    /// <summary>Samples a growing pose and then keeps it intact, including during a global pause.</summary>
    public void Advance(float delta)
    {
        _age = Mathf.Min(_holdAt, _age + Mathf.Max(0, delta));
        _track.TrackTime = _age; _animation.Update(0); _renderer.UpdateMesh();
    }

    /// <summary>Removes visible geometry immediately before deferred GameObject destruction.</summary>
    public void Clear()
    {
        _animation.AnimationState.ClearTracks();
        _renderer.GetComponent<MeshRenderer>().enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
