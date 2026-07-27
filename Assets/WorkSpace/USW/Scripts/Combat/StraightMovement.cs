using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 직선(Lerp) 이동 — 기존 Projectile의 기본 이동(0.35초 고정 직선)과 동일한 동작.
/// </summary>
[Serializable]
[DisplayName("직선 이동")]
public class StraightMovement : MovementBase
{
    public float duration = 0.35f;

    public override async UniTask MoveAsync(Transform movingTransform, Vector3 from, Vector3 to, CancellationToken token)
    {
        from.z = 0f;
        to.z = 0f;
        float elapsed = 0f;
        Quaternion baseRotation = GetBaseRotation(movingTransform);

        // 직선 이동은 진행 방향이 고정이므로 시작 시 한 번만 회전을 적용한다.
        FaceDirection(movingTransform, to - from, baseRotation);

        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            movingTransform.position = Vector3.Lerp(from, to, t);

            await UniTask.Yield(token);
        }

        movingTransform.position = to;
    }
}
