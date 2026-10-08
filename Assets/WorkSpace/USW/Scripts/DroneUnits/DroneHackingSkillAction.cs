using System;
using System.Collections.Generic;

/// <summary>해킹 전용 저작 데이터를 사용하며 유닛 수명에 묶인 생산/소비 시전을 시작한다.</summary>
[Serializable]
[DisplayName("드론 해킹")]
public sealed class DroneHackingSkillAction : ISkillAction
{
    /// <inheritdoc />
    public void Execute(UnitBase caster, UnitCombatComponent combat, List<IEffect> hitEffects, List<IAdditionalEffect> additionalEffects)
    {
        if (caster is Drone_Deltan producer) producer.CastHacking();
        else if (caster is Drone_Gamman consumer) consumer.CastHacking();
    }
}
