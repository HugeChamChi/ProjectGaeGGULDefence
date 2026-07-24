using UnityEngine;

/// <summary>스킬/공격 투사체가 적중했을 때의 결과(데미지, 디버프 등)를 나타내는 인터페이스.</summary>
public interface IHitEffect
{
    void Apply(UnitBase caster, BossBase target, Vector3 hitPosition);
}
