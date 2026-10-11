using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 레일건 투사체 (임시 — 디시그망). 날아가지 않고 총구에서 _chargeSeconds 동안 충전한 뒤 착탄점에 즉시 적중한다.
/// 시각은 자식 RailgunBeamFx가 담당하고, 적중 판정/피해는 기존 Projectile 경로(OnHit → onComplete)를 그대로 쓴다.
/// ProjectileData.projectilePrefab으로 지정해 쓰며 ProjectilePool이 발사마다 생성/파괴한다.
/// </summary>
public class RailgunProjectile : Projectile
{
    [Tooltip("총구 충전 시간 — 이 시간이 지나야 빔이 나가고 피해가 들어간다")]
    [SerializeField] private float _chargeSeconds = 0.4f;
    [Tooltip("총구 위치 보정 (발사 시작 위치 기준, 월드)")]
    [SerializeField] private Vector2 _muzzleOffset = Vector2.zero;
    [SerializeField] private RailgunBeamFx _fx;

    protected override async UniTask MoveAsync(Vector3 target, CancellationToken token)
    {
        try
        {
            Vector3 from = transform.position + (Vector3)_muzzleOffset;
            float scale = BaseScale.x > 1e-4f ? transform.localScale.x / BaseScale.x : 1f;
            if (_fx != null)
            {
                _fx.Play(transform, from, target, _chargeSeconds, scale);
                _fx = null; // 분리된 연출은 스스로 정리한다. 이 투사체는 한 발만 쓴다.
            }

            if (_chargeSeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(_chargeSeconds), cancellationToken: token);

            transform.position = target;
            OnHit(target);
            _onComplete?.Invoke(this);
        }
        catch (OperationCanceledException) { }
    }
}
