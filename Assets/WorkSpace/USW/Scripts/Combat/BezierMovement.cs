using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 포물선(2차 베지어 곡선) 이동 — from→to 사이가 곧게 가지 않고 부풀어오르는 궤적을 그린다.
/// 곡선 정점은 from→to 수직 방향으로 arcHeight만큼, 진행 방향으로 arcSkew 비율만큼 치우쳐 계산된다.
/// </summary>
[Serializable]
public class BezierMovement : IMovement
{
    [Tooltip("이동에 걸리는 시간(초)")]
    public float duration = 0.5f;

    [Tooltip("곡선이 부풀어오르는 높이 (from→to 수직 방향, 0이면 직선과 동일)")]
    public float arcHeight = 3f;

    [Tooltip("곡선 정점이 진행 방향으로 치우치는 비율 (0 = 정중앙, -1 = 시작점 쪽, 1 = 도착점 쪽)")]
    [Range(-1f, 1f)]
    public float arcSkew = 0f;

    public async UniTask MoveAsync(Transform movingTransform, Vector3 from, Vector3 to, CancellationToken token)
    {
        from.z = 0f;
        to.z = 0f;

        Vector3 direction = to - from;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f).normalized;
        Vector3 control = Vector3.Lerp(from, to, 0.5f + arcSkew * 0.5f) + perpendicular * arcHeight;

        float elapsed = 0f;
        Vector3 prevPos = from;

        while (elapsed < duration)
        {
            token.ThrowIfCancellationRequested();
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 pos = QuadraticBezier(from, control, to, t);
            movingTransform.position = pos;

            Vector3 dir = pos - prevPos;
            if (dir.sqrMagnitude > 0.0001f)
                movingTransform.up = dir.normalized;
            prevPos = pos;

            await UniTask.Yield(token);
        }

        movingTransform.position = to;
    }

    private static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }
}
