using UnityEngine;

/// <summary>
/// 풀링된 투사체가 다시 꺼내질 때 이전 발사의 트레일 잔상이 새 발사 위치까지 선으로 이어지지 않도록
/// 하위 TrailRenderer를 모두 비운다. 트레일을 가진 투사체 프리팹 루트에 부착.
/// </summary>
public class TrailRendererReset : MonoBehaviour
{
    private TrailRenderer[] _trails;

    private void Awake()
    {
        _trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    private void OnEnable() => Clear();

    private void OnDisable() => Clear();

    /// <summary>하위 트레일의 점을 모두 지운다.</summary>
    public void Clear()
    {
        if (_trails == null) return;
        foreach (var trail in _trails)
            if (trail != null) trail.Clear();
    }
}
