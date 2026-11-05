using UnityEngine;

/// <summary>연출 소유 난수. 호출 횟수나 프레임 수가 전투 추첨의 Unity 난수 상태를 바꾸지 않는다.</summary>
public sealed class PresentationRandom
{
    private readonly System.Random _random = new System.Random(System.Guid.NewGuid().GetHashCode());

    /// <summary>0 이상 1 미만의 연출 값.</summary>
    public float Value => (float)_random.NextDouble();

    /// <summary>최솟값 이상, 최댓값 미만 정수.</summary>
    public int Range(int minimum, int maximum) => _random.Next(minimum, maximum);

    /// <summary>지정 범위의 연출 값.</summary>
    public float Range(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, Value);

    /// <summary>DOTween이 흔들림 경로를 동기 생성할 때 전투 난수 상태를 보존한다. await/게임 판정 콜백을 넘기지 않는다.</summary>
    public static T CreateTween<T>(System.Func<T> create) where T : DG.Tweening.Tween
    {
        var state = Random.state;
        try { return create(); }
        finally { Random.state = state; }
    }

    /// <summary>단위 원 안에서 면적에 균일하게 분포한 연출 위치.</summary>
    public Vector2 InsideUnitCircle
    {
        get
        {
            float angle = Value * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
