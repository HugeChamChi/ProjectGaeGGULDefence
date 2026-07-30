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
    [Tooltip("시트/코드에서 이 데이터를 직접 참조 대신 id로 찾아 쓸 때 사용하는 식별자. ProjectileDataTable에 등록해야 id로 조회됩니다.")]
    public int id;

    [BoxGroup("투사체 프리팹 (모두 비워두면 ProjectilePool의 기본 투사체 사용)")]
    [Tooltip("직접 프리팹 참조. 비워두고 아래 주소만 채우면 Addressable 주소로 로드합니다.")]
    public Projectile projectilePrefab;
    [BoxGroup("투사체 프리팹 (모두 비워두면 ProjectilePool의 기본 투사체 사용)")]
    [Tooltip("projectilePrefab이 비어있을 때 사용할 Addressable 주소")]
    public string projectileAddress;

    [BoxGroup("이동")]
    [SerializeReference, SelectableReference]
    public IMovement movement = new StraightMovement();

    [BoxGroup("이펙트 (비워두면 사용 안 함)")]
    [SerializeReference, SelectableReference]
    public IEffectSpawner fireEffect;

    [BoxGroup("이펙트 (비워두면 사용 안 함)")]
    [SerializeReference, SelectableReference]
    public IEffectSpawner hitEffect;
}
