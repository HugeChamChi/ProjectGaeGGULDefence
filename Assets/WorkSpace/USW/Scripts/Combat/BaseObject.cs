using UnityEngine;

/// <summary>
/// 오브젝트 풀링/재사용되는 MonoBehaviour의 공통 상위 클래스.
/// Awake()는 실제 인스턴스 생성 시 단 한 번만 호출되므로, 그 시점의 프리팹 원본 localScale을
/// BaseScale로 고정해 둔다. 이후 크기 배율을 적용할 때는 항상 ApplyScale()로 BaseScale을
/// 기준으로 다시 계산하도록 강제해, transform.localScale에 매번 곱연산해 재사용될 때마다
/// 크기가 누적되어 계속 커지는 버그(예: 기존 Projectile.Launch())를 원천 차단한다.
/// Projectile 외에 풀링/재사용되는 다른 오브젝트도 이 클래스를 상속하면 동일하게 보호받는다.
/// </summary>
public abstract class BaseObject : MonoBehaviour
{
    /// <summary>Awake() 시점(프리팹 원본)의 localScale.</summary>
    protected Vector3 BaseScale { get; private set; }

    protected virtual void Awake()
    {
        BaseScale = transform.localScale;
    }

    /// <summary>BaseScale에 multiplier를 곱한 값을 transform.localScale에 적용한다.</summary>
    protected void ApplyScale(float multiplier)
    {
        transform.localScale = BaseScale * multiplier;
    }
}
