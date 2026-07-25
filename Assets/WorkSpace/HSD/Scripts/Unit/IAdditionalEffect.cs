using UnityEngine;

/// <summary>데미지 등 게임 상태에는 관여하지 않는 부가 연출(카메라 흔들림 등)을 나타내는 인터페이스.</summary>
public interface IAdditionalEffect
{
    void Apply(UnitBase caster, BossBase target, Vector3 hitPosition);
}
