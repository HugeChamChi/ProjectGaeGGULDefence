using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 미사일 형태의 유도 이동 — 초기에 무작위 각도로 쏘아진 뒤 타겟을 향해 가속하며 유도된다.
/// 기존 MissileProjectile(Projectile 상속 서브클래스)의 MoveAsync 알고리즘을 그대로 옮긴 것.
/// </summary>
[Serializable]
[DisplayName("유도 미사일")]
public class GuidedMissileMovement : MovementBase
{
    [Tooltip("초기 발사 속도 (Unit/s)")]
    public float startSpeed = 8.0f;
    [Tooltip("최대 비행 속도 (Unit/s)")]
    public float maxSpeed = 25.0f;
    [Tooltip("가속도 (Unit/s^2)")]
    public float acceleration = 20.0f;
    [Tooltip("타겟을 향해 방향을 꺾는 유도 성능(회전 속도)")]
    public float turnSpeed = 4f;
    [Tooltip("발사 시 퍼지는 최대 무작위 각도 (0이면 무조건 타겟 방향 직선)")]
    public float randomAngleMax = 60f;

    public override async UniTask MoveAsync(Transform movingTransform, Vector3 from, Vector3 to, CancellationToken token)
    {
        Vector3 currentPos = from;
        to.z = currentPos.z;
        movingTransform.position = currentPos;
        Quaternion baseRotation = GetBaseRotation(movingTransform);

        // 미사일 사출 시 프리팹 원본 방향(baseRotation의 up)을 기준으로 퍼지며 사출된 후 유도됩니다.
        // (풀링으로 재사용된 경우 movingTransform.up은 이전 발사 때 남은 값일 수 있어 사용하지 않는다.)
        Vector3 spawnUp = baseRotation * Vector3.up;
        if (spawnUp.sqrMagnitude < 0.001f) spawnUp = Vector3.up;

        float randomAngle = UnityEngine.Random.Range(-randomAngleMax, randomAngleMax);
        Vector3 initialDir = Quaternion.Euler(0, 0, randomAngle) * spawnUp;
        Vector3 currentVelocity = initialDir.normalized * startSpeed;

        FaceDirection(movingTransform, currentVelocity, baseRotation);

        float maxLifetime = 3.0f; // 최대 비행 시간 (안전 타임아웃)
        float lifetime = 0f;

        while (lifetime < maxLifetime)
        {
            token.ThrowIfCancellationRequested();

            float dt = Time.deltaTime;
            lifetime += dt;

            Vector3 desiredDir = (to - currentPos).normalized;

            // 가속
            float currentSpeed = currentVelocity.magnitude;
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * dt);

            // 유도 (RotateTowards: 라디안 단위 회전속도)
            Vector3 currentDir = currentVelocity.normalized;
            // turnSpeed가 라디안/초 단위가 되도록 Mathf.Deg2Rad 또는 충분한 회전각 보정
            Vector3 newDir = Vector3.RotateTowards(currentDir, desiredDir, turnSpeed * Mathf.Deg2Rad * 60f * dt, 0f);

            currentVelocity = newDir * currentSpeed;
            currentPos += currentVelocity * dt;
            movingTransform.position = currentPos;

            // 진행 방향에 맞춰 회전 (lookAtTarget == false면 회전하지 않음)
            FaceDirection(movingTransform, currentVelocity, baseRotation);

            // 타겟 도달 또는 타겟 오버슈트(지나침) 체크
            float distToTarget = Vector3.Distance(to, currentPos);
            float stepDist = currentSpeed * dt;

            // 목표점에 거의 도달했거나 한 프레임 이동 거리 이내로 지나친 경우
            if (distToTarget <= Mathf.Max(0.5f, stepDist * 1.5f))
            {
                movingTransform.position = to;
                return;
            }

            await UniTask.Yield(token);
        }

        movingTransform.position = to;
    }
}
