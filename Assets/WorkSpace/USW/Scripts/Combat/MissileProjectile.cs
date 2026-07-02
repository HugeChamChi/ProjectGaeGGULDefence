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
            
            // 처음에는 목표물 방향을 기준으로 무작위 각도(±_randomAngleMax)로 발사
            Vector3 toTarget = (target - currentPos).normalized;
            if (toTarget.sqrMagnitude < 0.001f) toTarget = Vector3.up;

            float randomAngle = UnityEngine.Random.Range(-_randomAngleMax, _randomAngleMax);
            
            // 타겟 방향을 기준으로 randomAngle만큼 회전한 방향이 초기 발사 방향
            Vector3 initialDir = Quaternion.Euler(0, 0, randomAngle) * toTarget;
            Vector3 currentVelocity = initialDir * _startSpeed;
            
            // 투사체의 머리(위쪽)가 이동 방향을 바라보게 설정
            transform.up = currentVelocity.normalized;

            while (true)
            {
                token.ThrowIfCancellationRequested();
                
                float dt = Time.deltaTime;
                Vector3 desiredDir = (target - currentPos).normalized;
                
                // 가속
                float currentSpeed = currentVelocity.magnitude;
                currentSpeed = Mathf.MoveTowards(currentSpeed, _maxSpeed, _acceleration * dt);

                // 유도 (방향 전환)
                Vector3 currentDir = currentVelocity.normalized;
                Vector3 newDir = Vector3.Slerp(currentDir, desiredDir, _turnSpeed * dt);
                
                currentVelocity = newDir * currentSpeed;
                currentPos += currentVelocity * dt;
                transform.position = currentPos;
                
                // 진행 방향에 맞춰 회전 (항상 이동 방향을 바라보도록)
                if (currentVelocity.sqrMagnitude > 0.001f)
                {
                    transform.up = currentVelocity.normalized;
                }

                // 타겟 도달 체크 (남은 거리가 이번 프레임 이동 거리보다 짧으면 도달)
                float moveDist = currentSpeed * dt;
                if (Vector3.SqrMagnitude(target - currentPos) <= moveDist * moveDist)
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
