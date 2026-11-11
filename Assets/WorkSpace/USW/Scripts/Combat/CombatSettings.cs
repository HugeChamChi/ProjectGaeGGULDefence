using UnityEngine;

/// <summary>씬이 사용하는 GameConfig의 불변 전투 기본값. 선택지와 토템 타격이 같은 원본을 공유한다.</summary>
public sealed class CombatSettings
{
    private const float MaximumVariance = .5f;
    /// <summary>선택지 계산 이전의 기본 치명 확률.</summary>
    public float BaseCritChance { get; }
    /// <summary>일반 피해의 난수 편차.</summary>
    public float DamageVariance { get; }
    /// <summary>치명 피해의 난수 편차.</summary>
    public float CritDamageVariance { get; }
    /// <summary>현재 등급 기본 공속 대비 최종 공격 빈도의 최대 배율.</summary>
    public float MaxAttackSpeedMultiplier { get; }
    /// <summary>보너스를 더하기 전의 기본 치명타 피해 배율.</summary>
    public float BaseCritDamageMultiplier { get; }
    /// <summary>설정이 없을 때의 기본 치명타 피해 배율 (GameConfig 기본값과 같다).</summary>
    public const float DefaultBaseCritDamageMultiplier = 1.5f;
    /// <summary>설정이 없을 때의 공속 배율 상한 (GameConfig 기본값과 같다).</summary>
    public const float DefaultMaxAttackSpeedMultiplier = 8f;
    /// <summary>저작 설정을 복사한다. 설정 없는 독립 검사에서는 기존 기본값 0을 사용한다.</summary>
    public CombatSettings(GameConfig config)
    {
        BaseCritChance = config != null ? GGD.BackendCharts.RunBalance.Number(GGD.BackendCharts.RunBalance.CombatBaseCritChance, config.baseCritChance) : 0f;
        DamageVariance = config != null ? Mathf.Clamp(GGD.BackendCharts.RunBalance.Number(GGD.BackendCharts.RunBalance.CombatDamageVariance, config.damageVariance), 0f, MaximumVariance) : 0f;
        CritDamageVariance = config != null ? Mathf.Clamp(GGD.BackendCharts.RunBalance.Number(GGD.BackendCharts.RunBalance.CombatCritDamageVariance, config.critDamageVariance), 0f, MaximumVariance) : 0f;
        BaseCritDamageMultiplier = config != null ? Mathf.Max(1f, GGD.BackendCharts.RunBalance.Number(GGD.BackendCharts.RunBalance.CombatBaseCritDamageMultiplier, config.baseCritDamageMultiplier)) : DefaultBaseCritDamageMultiplier;
        MaxAttackSpeedMultiplier = config != null ? Mathf.Max(1f, GGD.BackendCharts.RunBalance.Number(GGD.BackendCharts.RunBalance.CombatMaxAttackSpeedMultiplier, config.maxAttackSpeedMultiplier)) : DefaultMaxAttackSpeedMultiplier;
    }
}
