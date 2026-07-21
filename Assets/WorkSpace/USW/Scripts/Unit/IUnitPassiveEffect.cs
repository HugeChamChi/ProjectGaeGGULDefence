/// <summary>유닛 고유 패시브 효과 인터페이스.</summary>
public interface IUnitPassiveEffect
{
    /// <summary>target의 kind 스탯에 더해질 가산 보너스를 반환한다 (0.1 = +10%).</summary>
    float GetBonus(StatKind kind, UnitBase target, UnitDependencies deps, float bonusScale = 1f);
}
