using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfig : ScriptableObject
{
    public int   gridColumns       = 6;
    public int   gridRows          = 4;
    public float countdownSeconds  = 30f;
    public float bossSpawnDelaySeconds = 5f;
    public float cellSize          = 1.5f;

    [Header("Economy")]
    public float startingFood = 20f;   // food given at game start
    public float placeCost    = 10f;   // cost to place one unit

    [Header("Combat")]
    [Tooltip("모든 유닛·드론 공통 기본 치명타 확률 (0~1). 레벨업·토템 보너스는 이 위에 더한다.\n" +
             "엑셀 GameBalance.xlsx CombatCommon!baseCritChancePct → Tools/USW/Balance/CombatCommon Excel Import")]
    [Range(0f, 1f)] public float baseCritChance = 0f;

    [Header("Visuals")]
    public UnitTierPalette tierPalette; // 등급별 비주얼 설정
}