using UnityEngine;

/// <summary>
/// 특정 위치에서 재생되는 연출(파티클, 사운드, 카메라 흔들림 등)을 정의하는 인터페이스.
/// ProjectileData에 [SerializeReference]로 담겨 발사 이펙트(fireEffect)/피격 이펙트(hitEffect)로 쓰인다.
/// (IMovement와 대칭되는 컴포지션 슬롯)
/// </summary>
public interface IEffectSpawner
{
    void Play(Vector3 position, Transform parent, AudioManager audioManager);
}
