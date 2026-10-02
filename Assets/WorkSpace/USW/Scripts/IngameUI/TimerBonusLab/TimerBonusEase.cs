using UnityEngine;

/// <summary>타이머 보너스 실험실 이징 — HTML 시안과 같은 곡선. t는 0~1.</summary>
public static class TimerBonusEase
{
    /// <summary>빠르게 출발해 부드럽게 멈춤.</summary>
    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    /// <summary>천천히 출발해 가속.</summary>
    public static float InCubic(float t) => t * t * t;

    /// <summary>약한 가속.</summary>
    public static float InQuad(float t) => t * t;

    /// <summary>거의 즉시 도달 후 꼬리만 길게.</summary>
    public static float OutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

    /// <summary>목표를 지나쳤다 돌아옴. s가 클수록 많이 지나침.</summary>
    public static float OutBack(float t, float s = 1.70158f)
    {
        float u = t - 1f;
        return 1f + (s + 1f) * u * u * u + s * u * u;
    }

    /// <summary>감쇠 스프링 — k=0에서 1, 진동하며 0으로. k는 초.</summary>
    public static float Spring(float k, float frequency = 18f, float damping = 6f)
        => k < 0f ? 0f : Mathf.Exp(-damping * k) * Mathf.Cos(frequency * k);
}
