using UnityEngine;

/// <summary>
/// 인게임 씬 한정 싱글톤 기반 클래스
/// 씬이 종료되면 함께 소멸 — DontDestroyOnLoad 미적용
/// 사용 대상: GameManager, WaveManager, BossManager, UIManager 등 인게임 전용 매니저
/// </summary>
public abstract class InGameSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError($"[InGameSingleton] {typeof(T).Name} not found in scene. Ensure it is present in the Inspector.");

            return _instance;
        }
        protected set => _instance = value;
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
