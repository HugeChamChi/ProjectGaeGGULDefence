using System;
using System.Collections.Generic;

/// <summary>여러 PassiveData를 하나로 묶는 조합 패시브 효과.</summary>
[Serializable]
[DisplayName("패시브 묶음")]
public class CombinedPassiveEffect : IUnitPassiveEffect
{
    public List<PassiveData> passives = new List<PassiveData>();

    public float GetBonus(StatKind kind, UnitBase target, UnitDependencies deps, float bonusScale = 1f)
    {
        float bonus = 0f;
        if (passives == null) return bonus;

        foreach (var passive in passives)
        {
            if (passive != null) bonus += passive.GetAllBonus(kind, target, deps, bonusScale);
        }
        return bonus;
    }
}
