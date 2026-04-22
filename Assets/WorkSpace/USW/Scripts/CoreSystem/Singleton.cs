using UnityEngine;

/// <summary>
/// 씬 전환 후에도 유지되는 전역 싱글톤 기반 클래스
/// DontDestroyOnLoad 적용 — 앱 전체 생명주기 동안 유지
/// 사용 대상: ResourcesManager, PoolManager, SceneChangeManager 등
/// </summary>
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance => _instance;

    /// <summary>
    /// 런타임에 싱글톤 인스턴스를 생성하고 DontDestroyOnLoad 등록
    /// Manager.cs의 RuntimeInitializeOnLoadMethod에서 호출
    /// </summary>
    public static T CreateInstance()
    {
        if (_instance != null) return _instance;

        var go = new GameObject(typeof(T).Name);
        _instance = go.AddComponent<T>();
        DontDestroyOnLoad(go);
        return _instance;
    }

    /// <summary>
    /// 인스턴스 명시적 해제 (씬 전환 시 특정 매니저만 제거할 때 사용)
    /// </summary>
    public static void ReleaseInstance()
    {
        if (_instance == null) return;
        Destroy(_instance.gameObject);
        _instance = null;
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }
}
