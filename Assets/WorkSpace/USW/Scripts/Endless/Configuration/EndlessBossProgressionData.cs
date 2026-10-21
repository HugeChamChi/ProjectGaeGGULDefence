using UnityEngine;

/// <summary>Endless role-based HP, defense and HP-bar authoring, independent of a selected boss's base stats.</summary>
[CreateAssetMenu(fileName = "EndlessBossProgression", menuName = "Game/Endless/Boss Progression")]
public sealed class EndlessBossProgressionData : ScriptableObject
{
    [SerializeField, Min(1)] private long _breatherBaseHp = 25000;
    [SerializeField, Min(1)] private int _firstGrowthRound = 2;
    [SerializeField, Min(1)] private int _intervalRounds = 1;
    [SerializeField, Min(1)] private int _hardReferenceRound = 3;
    [SerializeField, Min(1)] private int _breatherReferenceRound = 5;
    [SerializeField, Min(1)] private double _hardReferenceMultiplier = 1.10;
    [SerializeField, Min(1)] private long _hpPerLine = 10000;
    [SerializeField, Range(0, 99)] private double _breatherDamageReductionPercent = 5;
    [SerializeField, Range(0, 99)] private double _hardDamageReductionPercent = 10;
    [SerializeField] private DebuffSettings _defenseSettings;
    [SerializeField, Tooltip("False while the current SO/remote growth rate is only a trial input.")]
    private bool _growthRateFinalized;

    /// <summary>Whether design has accepted the numeric growth rate, rather than only the growth structure.</summary>
    public bool GrowthRateFinalized => _growthRateFinalized;

    /// <summary>Copies all role settings and the defense scale once, insulating a run from later asset edits.</summary>
    public bool TryCapture(out EndlessBossProgressionSnapshot snapshot, out string error)
    {
        snapshot = new EndlessBossProgressionSnapshot(_breatherBaseHp, _firstGrowthRound, _intervalRounds,
            _hardReferenceRound, _breatherReferenceRound, _hardReferenceMultiplier, _hpPerLine,
            _breatherDamageReductionPercent, _hardDamageReductionPercent,
            _defenseSettings != null ? _defenseSettings.DefenseScale : 0, _growthRateFinalized);
        return snapshot.TryValidate(out error);
    }
}
