using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 단일 투사체 — 시작 위치에서 목표 위치로 이동 후 풀로 반환.
/// ProjectilePool이 생성/관리하며 직접 Instantiate 하지 않는다.
/// </summary>
public class Projectile : MonoBehaviour
{
    private Action<Projectile> _onComplete;
    private Coroutine          _moveCoroutine;

    public void Launch(Vector3 from, Vector3 to, Action<Projectile> onComplete)
    {
        transform.position = from;
        _onComplete        = onComplete;

        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);

        _moveCoroutine = StartCoroutine(Move(to));
    }

    private IEnumerator Move(Vector3 target)
    {
        const float Duration = 0.35f;
        float   elapsed = 0f;
        Vector3 start   = transform.position;

        while (elapsed < Duration)
        {
            elapsed            += Time.deltaTime;
            transform.position  = Vector3.Lerp(start, target, elapsed / Duration);
            yield return null;
        }

        transform.position = target;
        _moveCoroutine     = null;
        _onComplete?.Invoke(this);
    }

    private void OnDisable()
    {
        if (_moveCoroutine == null) return;
        StopCoroutine(_moveCoroutine);
        _moveCoroutine = null;
    }
}
