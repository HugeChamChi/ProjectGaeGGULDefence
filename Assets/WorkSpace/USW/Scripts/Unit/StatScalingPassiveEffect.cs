using System;
using UnityEngine;

/// <summary>sourceStat이 sourceStepPercent(%)만큼 증가할 때마다 targetStat이 targetBonusPerStep(%) 증가하는 패시브 효과.</summary>
[Serializable]
[DisplayName("스탯 비례 패시브")]
public class StatScalingPassiveEffect : IUnitPassiveEffect
{
    public StatKind sourceStat = StatKind.ProjectileSize;
    public StatKind targetStat = StatKind.AttackPercent;

    [Tooltip("sourceStat이 이 값(%)만큼 증가할 때마다 targetStat 보너스가 1스텝 적용된다.")]
    public float sourceStepPercent = 10f;
    [Tooltip("스텝 1회당 증가하는 targetStat 보너스(%)")]
    public float targetBonusPerStep = 1f;

    public float GetBonus(StatKind kind, UnitBase target, UnitDependencies deps, float bonusScale = 1f)
    {
        if (kind != targetStat) return 0f;
        if (sourceStat == targetStat) return 0f; // 자기 자신을 참조하는 무한 재귀 방지
        if (sourceStepPercent <= 0f) return 0f;

        float sourceIncreasePct = Mathf.Max(0f, (target?.GetStatBonus(sourceStat) ?? 0f) * 100f);
        float steps = sourceIncreasePct / sourceStepPercent;

        return steps * (targetBonusPerStep / 100f) * bonusScale;
    }
}
