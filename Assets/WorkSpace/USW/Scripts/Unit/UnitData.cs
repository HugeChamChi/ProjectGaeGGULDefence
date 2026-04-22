using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "Game/UnitData")]

public class UnitData : ScriptableObject
{
    [Header("Info")]
    public int      unitType;
    public string   unitName;
    public UnitTier unitTier;
    public GameObject prefab;
    [TextArea] public string description;

    [Header("Gauge")]
    public float gaugeDuration  = 3f;
    public float gaugeMultiplier = 1f;

    [Header("On Gauge Full")]
    public float foodPerTick   = 10f;
    public int   attackDamage  = 0;     
}