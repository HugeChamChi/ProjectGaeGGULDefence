using UnityEngine;
using Alchemy.Inspector;

/// <summary>
/// 투사체의 이동/이펙트 조합을 정의하는 데이터. Projectile 프리팹은 하나로 공유되고,
/// 이 데이터를 주입받아 실제 동작(이동 방식, 발사/피격 이펙트)이 달라진다.
/// TotemData의 ITotemFunction/ITotemRange를 [SerializeReference]로 구성하는 것과 동일한 패턴.
/// </summary>
[CreateAssetMenu(fileName = "ProjectileData", menuName = "Game/ProjectileData")]
public class ProjectileData : ScriptableObject
{
    [BoxGroup("이동")]
    [SerializeReference, SelectableReference]
    public IMovement movement = new StraightMovement();

    [BoxGroup("이펙트 (비워두면 사용 안 함)")]
    [SerializeReference, SelectableReference]
    public IProjectileEffect fireEffect;

    [BoxGroup("이펙트 (비워두면 사용 안 함)")]
    [SerializeReference, SelectableReference]
    public IProjectileEffect hitEffect;
}
