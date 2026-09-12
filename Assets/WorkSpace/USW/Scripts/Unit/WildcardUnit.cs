/// <summary>TD1004가 생성하는 노말 합성 재료. 공격/스킬/식량 생산과 인구수 소비가 없다.</summary>
public sealed class WildcardUnit : UnitBase
{
    /// <inheritdoc />
    public override bool IsWildcardMergeUnit => true;
    /// <inheritdoc />
    public override bool CanBasicAttack => false;
    /// <inheritdoc />
    public override bool CanAutoSkill => false;
    /// <inheritdoc />
    public override bool IsFoodProductionBuffable => false;
    /// <inheritdoc />
    public override float GetBaseFoodPerSecond() => 0f;
    /// <inheritdoc />
    public override float CurrentFoodProductionPerSecond => 0f;
}
