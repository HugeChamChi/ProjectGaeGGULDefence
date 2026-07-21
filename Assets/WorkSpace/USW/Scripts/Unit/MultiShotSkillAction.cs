using System;
using UnityEngine;

/// <summary>공격력에 atkCoefficient를 곱한 데미지를 shotCount번 발사하는 스킬 액션.</summary>
[Serializable]
public class MultiShotSkillAction : ISkillAction
{
    [Tooltip("동시/연속 발사 횟수")]
    public int shotCount = 1;
    [Tooltip("발당 데미지 = 공격력 × 이 값 (1 = 공격력 그대로)")]
    public float atkCoefficient = 1f;

    public void Execute(UnitBase caster, UnitCombatComponent combat)
    {
        int damage = Mathf.RoundToInt(caster.GetAttackDamage() * atkCoefficient);
        for (int i = 0; i < shotCount; i++)
            combat.LaunchProjectile(damage);
    }
}
