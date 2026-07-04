using UnityEngine;

/// <summary>
/// BossManager가 보스 소환 시 자동으로 AddComponent 해주는 타겟 영역 정의.
/// GetRandomWorldPosition()으로 400×400 영역 내 랜덤 월드 좌표 반환.
/// </summary>
public class BossAreaTarget : MonoBehaviour
{
    [SerializeField] private float _width  = 1f;
    [SerializeField] private float _height = 3f;

    /// <summary>보스 중심 기준 위쪽(+Y) 피격 위치 반환</summary>
    public Vector3 GetRandomWorldPosition()
    {
        float hw = _width * 0.5f;

        return transform.position + new Vector3(
            Random.Range(-hw, hw),
            Random.Range(0f, _height),
            0f
        );
    }
}
