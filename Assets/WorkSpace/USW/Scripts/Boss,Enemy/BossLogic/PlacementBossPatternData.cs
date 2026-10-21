using UnityEngine;

/// <summary>Connects a placement mechanic to the shared boss scheduler and a Spine skill.</summary>
[CreateAssetMenu(menuName = "Game/Boss/Placement Pattern")]
public sealed class PlacementBossPatternData : BossPatternData
{
    /// <summary>Authored targeting, warning and penalty rules shared with the pattern library.</summary>
    public PatternLabData[] Candidates;
    /// <summary>HP gates shared by all five possible Spine skills.</summary>
    public PlacementBossPhaseData Phases;
    /// <summary>Material used by the mechanic's battlefield warnings.</summary>
    public Material TelegraphMaterial;
    /// <summary>Installed Spine duration, used to avoid cutting off the animation after an early success.</summary>
    public float NativeDuration;
    /// <inheritdoc/>
    public override float CastDuration => NativeDuration;

    /// <summary>Selects a random valid mechanic at cast time, trying alternatives if its board prerequisites fail.</summary>
    public bool TryStartRandom(PatternLabDriver driver)
    {
        if (driver == null || Candidates == null || Candidates.Length == 0) return false;
        // Shuffle indices locally: each candidate is equally likely to be the first valid mechanic.
        var indices = new int[Candidates.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = 0; i < indices.Length; i++)
        {
            int selected = Random.Range(i, indices.Length);
            (indices[i], indices[selected]) = (indices[selected], indices[i]);
            if (Candidates[indices[i]] != null && driver.TryStart(Candidates[indices[i]])) return true;
        }
        return false;
    }
}
