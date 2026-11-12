using UnityEngine;

[CreateAssetMenu(fileName = "StaminaConfig", menuName = "Game/StaminaConfig")]
public class StaminaConfig : ScriptableObject
{
    [Tooltip("스태미나 1 회복에 걸리는 시간(초). 기획 기준 10분 = 600초")]
    public int recoveryIntervalSeconds = 600;

    [Tooltip("신규 유저 생성 시 기본 최대 스태미나")]
    public int defaultMaxStamina = 30;

    [Header("다이아 즉시 회복")]
    [Tooltip("에너지 아이콘 팝업에서 다이아 소모로 즉시 회복시키는 스태미나 양")]
    public int refillAmount = 30;

    [Tooltip("스태미나 즉시회복 1회당 소모되는 다이아 수 (기획 확정값 아님 - 밸런스 확정 후 조정 필요)")]
    public int diamondCostForRefill = 50;

    [Header("스테이지 진입")]
    [Tooltip("파티 선택 후 게임 시작(스테이지 진입) 시 소모되는 스태미나")]
    public int stageEntryCost = 5;

    // The fields above are the bundled values (Editor, tutorial, before login). Gameplay and UI read the properties below,
    // which return the DEV_/LIVE_OutgameBalance chart values once the chart is installed (TODO G12).
    public int CurrentRecoveryIntervalSeconds => GGD.BackendCharts.OutgameBalance.Integer(GGD.BackendCharts.OutgameBalance.StaminaRecoveryIntervalSeconds, recoveryIntervalSeconds);
    public int CurrentDefaultMaxStamina => GGD.BackendCharts.OutgameBalance.Integer(GGD.BackendCharts.OutgameBalance.StaminaDefaultMax, defaultMaxStamina);
    public int CurrentRefillAmount => GGD.BackendCharts.OutgameBalance.Integer(GGD.BackendCharts.OutgameBalance.StaminaRefillAmount, refillAmount);
    public int CurrentDiamondCostForRefill => GGD.BackendCharts.OutgameBalance.Integer(GGD.BackendCharts.OutgameBalance.StaminaRefillDiamondCost, diamondCostForRefill);
    public int CurrentStageEntryCost => GGD.BackendCharts.OutgameBalance.Integer(GGD.BackendCharts.OutgameBalance.StaminaStageEntryCost, stageEntryCost);
}
