using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 투사체의 이동 방식을 정의하는 인터페이스. ProjectileData에 [SerializeReference]로 담겨
/// 직선/유도/포물선 등 이동 로직을 다형적으로 교체할 수 있다.
/// (TotemData의 ITotemFunction/ITotemRange와 동일한 컴포지션 패턴)
/// </summary>
public interface IMovement
{
    /// <summary>movingTransform을 from에서 to까지 이동시킨다. 완료 시 movingTransform.position은 to와 같아야 한다.</summary>
    UniTask MoveAsync(Transform movingTransform, Vector3 from, Vector3 to, CancellationToken token);
}
