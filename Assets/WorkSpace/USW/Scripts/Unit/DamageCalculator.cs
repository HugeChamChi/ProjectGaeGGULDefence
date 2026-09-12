using System;
using UnityEngine;

/// <summary>공격자 측 기존 정수 계산과 보스 측 최종 고정소수 피해 경계.</summary>
public static class DamageCalculator
{
    /// <summary>일반 피해에 로그 방어·아머·받피증을 한 번 적용하고 남은 HP로 제한한다.</summary>
    public static long Calculate(decimal baseDamage, double defense, double defenseScale,
        double armorFactor, decimal multiplier, long remainingUnits)
    {
        if (baseDamage < 0 || multiplier < 1 || remainingUnits < 0 || defense < 0 || defenseScale <= 0 || armorFactor < 1 ||
            double.IsNaN(defense) || double.IsInfinity(defense) || double.IsNaN(defenseScale) || double.IsInfinity(defenseScale) ||
            double.IsNaN(armorFactor) || double.IsInfinity(armorFactor)) throw new ArgumentOutOfRangeException(nameof(baseDamage));
        decimal remaining = (decimal)remainingUnits / CombatHealth.Scale;
        if (baseDamage == 0 || remainingUnits == 0) return 0;
        decimal factor = (decimal)(1 / (1 + Math.Log(1 + defense / defenseScale) / armorFactor));
        decimal attenuated = baseDamage * factor;
        if (attenuated == 0) return 0;
        if (attenuated >= remaining / multiplier) return remainingUnits;
        return CombatHealth.ToUnits(attenuated * multiplier);
    }
    /// <summary>
    /// 최종 데미지 계산 시 소수점 처리 방식을 결정합니다.
    /// 현재는 반올림(Round)으로 적용되어 있으며, 추후 기획 변경 시 이곳에서 일괄 수정할 수 있습니다.
    /// </summary>
    public static int ApplyRounding(float rawDamage)
    {
        return Mathf.RoundToInt(rawDamage);
        // return Mathf.FloorToInt(rawDamage); // 버림
        // return Mathf.CeilToInt(rawDamage);  // 올림
    }
}
