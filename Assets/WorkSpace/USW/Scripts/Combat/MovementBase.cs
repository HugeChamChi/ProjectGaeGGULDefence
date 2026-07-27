using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// IMovement 구현체들의 공통 상위 클래스. 이동 방식마다 경로 계산 로직은 다르지만
/// "진행 방향을 바라보도록 회전"하는 처리는 동일하므로 FaceDirection() 헬퍼로 공유한다.
/// 직선 이동은 방향이 고정이라 시작 시 한 번만 호출하면 되고, 유도미사일/포물선은
/// 프레임마다 방향이 바뀌므로 매 프레임 호출한다.
/// </summary>
[Serializable]
public abstract class MovementBase : IMovement
{
    public abstract UniTask MoveAsync(Transform movingTransform, Vector3 from, Vector3 to, CancellationToken token);

    /// <summary>movingTransform에 붙은 BaseObject의 BaseRotation(프리팹 원본 회전값)을 반환한다.
    /// 풀링으로 재사용되어도 항상 이 값을 기준으로 회전을 다시 계산해야, 이전 발사에서 남은
    /// 회전이 누적되지 않는다(BaseObject.ApplyScale과 동일한 원칙). BaseObject가 없으면
    /// Quaternion.identity로 폴백.</summary>
    protected static Quaternion GetBaseRotation(Transform movingTransform)
    {
        var baseObject = movingTransform.GetComponent<BaseObject>();
        return baseObject != null ? baseObject.BaseRotation : Quaternion.identity;
    }

    /// <summary>baseRotation의 up축이 dir을 향하도록 회전시킨다
    /// (baseRotation 자체에 담긴 롤/기울기는 그대로 유지된다).</summary>
    protected void FaceDirection(Transform movingTransform, Vector3 dir, Quaternion baseRotation)
    {
        if (dir.sqrMagnitude <= 0.0001f) return;

        Vector3 baseUp = baseRotation * Vector3.up;
        movingTransform.rotation = Quaternion.FromToRotation(baseUp, dir.normalized) * baseRotation;
    }
}
