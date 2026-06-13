using System;
using VContainer;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 기본 투사체 (직선 이동) 및 상속을 위한 베이스 클래스.
/// ProjectilePool 또는 RM.Instantiate 등을 통해 소환됩니다.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Inject] protected TotemBuffManager _totemBuffManager;
    [Inject] protected AudioManager _audioManager;

    [Header("폭발 이펙트 (비워두면 사용 안 함)")]
    [SerializeField] protected GameObject _hitEffectPrefab;
    [SerializeField] protected float _hitEffectDuration = 1.0f;

    protected Action<Projectile>      _onComplete;
    protected CancellationTokenSource _moveCts;

    public virtual void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete)
    {
        StopMove();

        transform.position = from;
        transform.localScale = Vector3.one * (_totemBuffManager?.ProjectileSizeMultiplier ?? 1f);
        _onComplete        = onComplete;

        _moveCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        MoveAsync(to, _moveCts.Token).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
    }

    protected void StopMove()
    {
        if (_moveCts == null) return;
        _moveCts.Cancel();
        _moveCts.Dispose();
        _moveCts = null;
    }

    protected virtual async UniTask MoveAsync(Vector3 target, CancellationToken token)
    {
        try
        {
            const float Duration = 0.35f;
            float   elapsed = 0f;
            Vector3 start   = transform.position;

            while (elapsed < Duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed            += Time.deltaTime;
                float t = elapsed / Duration;
                
                transform.position = Vector3.Lerp(start, target, t);

                // 기본적으로 목표점을 향해 회전
                Vector3 dir = target - transform.position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.up = dir.normalized;
                }

                await UniTask.Yield(token);
            }

            transform.position = target;
            
            OnHit(target);

            _onComplete?.Invoke(this);
        }
        catch (OperationCanceledException) { }
    }

    protected virtual void OnHit(Vector3 pos)
    {
        _audioManager?.PlaySFX("05.Drone_Attack_Hit");

        if (_hitEffectPrefab != null)
        {
            var effect = RM.Instantiate(_hitEffectPrefab, pos, Quaternion.identity, transform.parent, true);
            if (effect != null)
            {
                RM.Destroy(effect, _hitEffectDuration);
            }
        }
    }

    protected virtual void OnDisable()
    {
        StopMove();
    }

    protected virtual void OnDestroy()
    {
        StopMove();
    }
}
