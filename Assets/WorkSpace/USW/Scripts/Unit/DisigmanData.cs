using UnityEngine;

/// <summary>Authored charge and countdown recovery, independent of drone hacking.</summary>
[CreateAssetMenu(fileName = "Disigman", menuName = "Game/Disigman")]
public sealed class DisigmanData : ScriptableObject
{
    /// <summary>Own basic attacks required for one skill; runtime minimum is one.</summary>
    public PerTierInt AttacksToCharge = new PerTierInt();
    /// <summary>Seconds immediately restored by one accepted skill.</summary>
    public PerTierFloat SecondsRecovered = new PerTierFloat();

    // The fields above are the bundled values (Editor, tutorial). Gameplay and UI read the members below,
    // which return the DEV_/LIVE_SkillBalance chart values once the chart is installed (TODO G12).
    public int GetAttacksToCharge(Tier tier) => Mathf.RoundToInt(GGD.BackendCharts.SkillBalance.Tiered(GGD.BackendCharts.SkillBalance.DisigmanAttacksToCharge, tier, AttacksToCharge.Get(tier)));
    public float GetSecondsRecovered(Tier tier) => GGD.BackendCharts.SkillBalance.Tiered(GGD.BackendCharts.SkillBalance.DisigmanSecondsRecovered, tier, SecondsRecovered.Get(tier));
    /// <summary>Current recovery seconds of all four tiers, for text that lists them together.</summary>
    public PerTierFloat CurrentSecondsRecovered => new PerTierFloat
    {
        normal = GetSecondsRecovered(Tier.Normal), rare = GetSecondsRecovered(Tier.Rare),
        epic = GetSecondsRecovered(Tier.Epic), legend = GetSecondsRecovered(Tier.Legend)
    };
}
