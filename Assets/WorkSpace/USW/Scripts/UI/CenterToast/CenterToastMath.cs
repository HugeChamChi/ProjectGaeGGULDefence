using UnityEngine;

/// <summary>중앙 알림 연출 곡선. t는 0~1(범위 밖은 잘림), Spring의 k는 초.</summary>
public static class CenterToastMath
{
    /// <summary>빠르게 출발해 부드럽게 멈춤.</summary>
    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

    /// <summary>거의 즉시 도착하고 꼬리만 길게 — 모던한 '스냅'.</summary>
    public static float OutExpo(float t)
    {
        t = Mathf.Clamp01(t);
        return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
    }

    /// <summary>천천히 출발해 가속.</summary>
    public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }

    /// <summary>약한 가속.</summary>
    public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }

    /// <summary>목표를 지나쳤다 돌아옴.</summary>
    public static float OutBack(float t, float s = 1.70158f)
    {
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + (s + 1f) * u * u * u + s * u * u;
    }

    /// <summary>감쇠 스프링 — k=0에서 1, 진동하며 0으로. k가 음수거나 무한대면 0.</summary>
    public static float Spring(float k, float frequency, float damping)
        => k < 0f || float.IsInfinity(k) ? 0f : Mathf.Exp(-damping * k) * Mathf.Cos(frequency * k);
}
