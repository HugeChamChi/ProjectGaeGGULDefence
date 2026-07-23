using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// IMovement 구현체들의 공통 상위 클래스. "이동 경로를 어떻게 계산하는가"(직선/포물선/유도)와
/// "이동 중 진행 방향을 바라보도록 회전할 것인가"는 서로 독립적인 축이라, 후자를 lookAtTarget
/// 플래그 하나로 두고 각 이동 방식이 공통으로 상속해서 쓴다.
/// (경로 계산 로직이 서로 전혀 다른 Straight/Bezier/GuidedMissile을 "회전 여부"만으로 클래스를
/// 쪼개면 로직이 중복되므로, 대신 여기서 FaceDirection() 헬퍼를 공유한다.)
/// </summary>
[Serializable]
public abstract class MovementBase : IMovement
{
    [Tooltip("이동 중 진행 방향을 바라보도록 회전할지 여부. 원형/구체 등 방향성이 없는 투사체 스프라이트라면 꺼도 무방합니다.")]
    public bool lookAtTarget = true;

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

    /// <summary>lookAtTarget이 켜져 있을 때만, baseRotation의 up축이 dir을 향하도록 회전시킨다
    /// (baseRotation 자체에 담긴 롤/기울기는 그대로 유지된다).</summary>
    protected void FaceDirection(Transform movingTransform, Vector3 dir, Quaternion baseRotation)
    {
        if (!lookAtTarget) return;
        if (dir.sqrMagnitude <= 0.0001f) return;

        Vector3 baseUp = baseRotation * Vector3.up;
        movingTransform.rotation = Quaternion.FromToRotation(baseUp, dir.normalized) * baseRotation;
    }
}
