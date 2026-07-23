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

    /// <summary>lookAtTarget이 켜져 있을 때만 movingTransform.up을 dir 방향으로 회전시킨다.</summary>
    protected void FaceDirection(Transform movingTransform, Vector3 dir)
    {
        if (!lookAtTarget) return;
        if (dir.sqrMagnitude > 0.0001f)
            movingTransform.up = dir.normalized;
    }
}
