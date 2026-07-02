using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

/// <summary>
/// 미사일 형태의 유도 포물선 궤적을 그리는 투사체.
/// 초기에 위쪽(혹은 무작위 각도)으로 쏘아진 뒤 타겟을 향해 가속하며 유도됩니다.
/// </summary>
public class MissileProjectile : Projectile
{
    [Header("미사일 유도 설정")]
    [Tooltip("초기 발사 속도 (Unit/s)")]
    [SerializeField] private float _startSpeed = 8.0f;
    [Tooltip("최대 비행 속도 (Unit/s)")]
    [SerializeField] private float _maxSpeed = 25.0f;
    [Tooltip("가속도 (Unit/s^2)")]
    [SerializeField] private float _acceleration = 20.0f;
    [Tooltip("타겟을 향해 방향을 꺾는 유도 성능(회전 속도)")]
    [SerializeField] private float _turnSpeed = 4f;
    [Tooltip("발사 시 퍼지는 최대 무작위 각도 (0이면 무조건 타겟 방향 직선)")]
    [SerializeField] private float _randomAngleMax = 60f;

    protected override async UniTask MoveAsync(Vector3 target, CancellationToken token)
    {
        try
        {
            Vector3 currentPos = transform.position;
            target.z = currentPos.z;
            
            // 미사일 사출 시 유닛 전방(transform.up)을 기준으로 퍼지며 사출된 후 유도됩니다.
            Vector3 spawnUp = transform.up; 
            if (spawnUp.sqrMagnitude < 0.001f) spawnUp = Vector3.up;

            float randomAngle = UnityEngine.Random.Range(-_randomAngleMax, _randomAngleMax);
            Vector3 initialDir = Quaternion.Euler(0, 0, randomAngle) * spawnUp;
            Vector3 currentVelocity = initialDir.normalized * _startSpeed;

            transform.up = currentVelocity.normalized;

            float maxLifetime = 3.0f; // 최대 비행 시간 (안전 타임아웃)
            float lifetime = 0f;

            while (lifetime < maxLifetime)
            {
                token.ThrowIfCancellationRequested();
                
                float dt = Time.deltaTime;
                lifetime += dt;

                Vector3 desiredDir = (target - currentPos).normalized;
                
                // 가속
                float currentSpeed = currentVelocity.magnitude;
                currentSpeed = Mathf.MoveTowards(currentSpeed, _maxSpeed, _acceleration * dt);

                // 유도 (RotateTowards: 라디안 단위 회전속도)
                Vector3 currentDir = currentVelocity.normalized;
                // _turnSpeed가 라디안/초 단위가 되도록 Mathf.Deg2Rad 또는 충분한 회전각 보정
                Vector3 newDir = Vector3.RotateTowards(currentDir, desiredDir, _turnSpeed * Mathf.Deg2Rad * 60f * dt, 0f);
                
                currentVelocity = newDir * currentSpeed;
                currentPos += currentVelocity * dt;
                transform.position = currentPos;
                
                // 진행 방향에 맞춰 회전
                if (currentVelocity.sqrMagnitude > 0.001f)
                {
                    transform.up = currentVelocity.normalized;
                }

                // 타겟 도달 또는 타겟 오버슈트(지나침) 체크
                float distToTarget = Vector3.Distance(target, currentPos);
                float stepDist = currentSpeed * dt;

                // 목표점에 거의 도달했거나 한 프레임 이동 거리 이내로 지나친 경우
                if (distToTarget <= Mathf.Max(0.5f, stepDist * 1.5f))
                {
                    transform.position = target;
                    break;
                }

                await UniTask.Yield(token);
            }

            OnHit(target);
            _onComplete?.Invoke(this);
        }
        catch (OperationCanceledException) { }
    }
}
