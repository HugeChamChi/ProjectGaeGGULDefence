using System.Collections.Generic;

/// <summary>스킬 발동 시 어떻게 발사하는지를 나타내는 인터페이스.</summary>
public interface ISkillAction
{
    void Execute(UnitBase caster, UnitCombatComponent combat, List<IEffect> hitEffects, List<IAdditionalEffect> additionalEffects);
}
