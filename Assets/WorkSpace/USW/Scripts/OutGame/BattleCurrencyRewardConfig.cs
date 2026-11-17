using System;
using UnityEngine;

/// <summary>
/// 일반 전투 종료 골드와 정수(UpgradeTokenA) 보상 단가. 계정 경험치와 같은 보스 진행량(1마리 완전 처치 = 1)에 곱한다.
/// 근거: 재화 설계 2026-11-07 6절 A안(진행량 비례), 정수 = 평균 한 판 2,000 / 표준 진행량,
/// 골드 = ECON01 Fable 검토 B-6(성장 0 계정 판당 약 636 / 표준 진행량). 표준 진행량 18.82는 TIME60 50판 측정(추정, 2026-11-14).
/// </summary>
[CreateAssetMenu(fileName = "BattleCurrencyRewardConfig", menuName = "Game/OutGame/Battle Currency Reward Config")]
public sealed class BattleCurrencyRewardConfig : ScriptableObject
{
    /// <summary>Resources 경로.</summary>
    public const string ResourcePath = "BattleCurrencyRewardConfig";

    [Tooltip("보스 1마리(진행량 1)당 골드")]
    [Min(0f)] public float GoldPerBoss = 158f;
    [Tooltip("보스 1마리(진행량 1)당 정수")]
    [Min(0f)] public float EssencePerBoss = 15.1f;
    [Tooltip("결과창 보상 더 받기가 쓰는 상점 광고 설정(광고 아이콘, 보상형 광고 ID)")]
    public GaeGGUL.Shop.UI.ShopCatalogSettings ShopSettings;

    /// <summary>진행량에 단가를 곱하고 재화별로 한 번 내린다. 영구 강화 보너스는 지급 경로(RewardInventory)가 더한다.</summary>
    public int Gold(decimal progress) => Floor(GGD.BackendCharts.OutgameBalance.Number(GGD.BackendCharts.OutgameBalance.BattleGoldPerBoss, GoldPerBoss), progress);
    /// <inheritdoc cref="Gold"/>
    public int Essence(decimal progress) => Floor(GGD.BackendCharts.OutgameBalance.Number(GGD.BackendCharts.OutgameBalance.BattleEssencePerBoss, EssencePerBoss), progress);

    private static int Floor(float perBoss, decimal progress)
    {
        if (progress <= 0 || perBoss <= 0) return 0;
        decimal value = decimal.Floor((decimal)perBoss * progress);
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }
}
