using UnityEngine;

/// <summary>
/// 투사체의 발사/도착 시점 이펙트(파티클, 사운드 등)를 정의하는 인터페이스.
/// ProjectileData에 [SerializeReference]로 담겨 발사 이펙트(fireEffect)/피격 이펙트(hitEffect)로 쓰인다.
/// (IMovement와 대칭되는 컴포지션 슬롯)
/// </summary>
public interface IProjectileEffect
{
    void Play(Vector3 position, Transform parent, AudioManager audioManager);
}
