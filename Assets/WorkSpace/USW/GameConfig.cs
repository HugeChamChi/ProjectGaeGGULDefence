using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfig : ScriptableObject
{
    public int   gridColumns       = 6;
    public int   gridRows          = 4;
    public float countdownSeconds  = 30f;
    public float cellSize          = 1.5f;


    [Header("Economy")]
    public float startingFood = 20f;   // food given at game start
    public float placeCost    = 10f;   // cost to place one unit
}