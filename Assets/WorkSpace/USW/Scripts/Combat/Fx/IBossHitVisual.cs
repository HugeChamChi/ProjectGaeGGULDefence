using Spine.Unity;

/// <summary>
/// Optional boss-side hint for <see cref="BossHitReaction"/> when the boss body is not the first renderer in its
/// children (for example Deertree, whose roots and vines are separate skeletons).
/// </summary>
public interface IBossHitVisual
{
    /// <summary>Skeleton of the boss body, or null while it is not created yet.</summary>
    SkeletonRenderer HitSkeleton { get; }
}
