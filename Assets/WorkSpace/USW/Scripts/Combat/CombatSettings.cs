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
    /// <summary>저작 설정을 복사한다. 설정 없는 독립 검사에서는 기존 기본값 0을 사용한다.</summary>
    public CombatSettings(GameConfig config)
    {
        BaseCritChance = config != null ? config.baseCritChance : 0f;
        DamageVariance = config != null ? Mathf.Clamp(config.damageVariance, 0f, MaximumVariance) : 0f;
        CritDamageVariance = config != null ? Mathf.Clamp(config.critDamageVariance, 0f, MaximumVariance) : 0f;
    }
}
