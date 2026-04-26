using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 단일 투사체 — 시작 위치에서 목표 위치로 이동 후 풀로 반환.
/// ProjectilePool이 생성/관리하며 직접 Instantiate 하지 않는다.
/// </summary>
public class Projectile : MonoBehaviour
{
    private Action<Projectile>      _onComplete;
    private CancellationTokenSource _moveCts;

    public void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete)
    {
        StopMove();

        transform.position = from;
        _onComplete        = onComplete;

        // OnDisable에서 수동 취소 가능하고,
        // 오브젝트 Destroy 시에도 자동 취소되도록 DestroyToken과 연결
        _moveCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        MoveAsync(to, _moveCts.Token).Forget(Debug.LogException);
    }

    private void StopMove()
    {
        if (_moveCts == null) return;
        _moveCts.Cancel();
        _moveCts.Dispose();
        _moveCts = null;
    }

    private async UniTask MoveAsync(Vector3 target, CancellationToken token)
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
                transform.position  = Vector3.Lerp(start, target, elapsed / Duration);
                await UniTask.Yield(token);
            }

            transform.position = target;
            _onComplete?.Invoke(this);
        }
        catch (OperationCanceledException) { }
    }

    private void OnDisable()
    {
        StopMove();
    }

    private void OnDestroy()
    {
        StopMove();
    }
}
