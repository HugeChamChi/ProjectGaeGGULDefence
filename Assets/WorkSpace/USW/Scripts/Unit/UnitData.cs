using UnityEngine;

[CreateAssetMenu(fileName = "UnitData", menuName = "Game/UnitData")]
public class UnitData : ScriptableObject
{
    [Header("Info")]
    public int        characterId;
    public int        unitType;
    public string     unitName;
    public Tier       unitTier;
    public UnitTribe  unitTribe;
    public Sprite     icon;
    public GameObject prefab;
    [TextArea] public string description;

    [Header("Attack")]
    public int   atk;           // 일반 공격력 — 1초마다
    public int   skillAtk;      // 스킬 공격력 — skillCooldown마다
    public float skillCooldown; // 스킬 쿨다운 (초)

    [Header("Skill")]
    public float foodPerTick = 10f; // 스킬 발동 시 식량 생산량

    [Header("Population")]
    public int populationCost = 1;
}
