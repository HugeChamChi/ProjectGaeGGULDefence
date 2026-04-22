using UnityEngine;
using BackEnd;

public class BackendManager : MonoBehaviour
{
    public static BackendManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsLoggedIn()
    {
        return Backend.IsLogin;
    }

    public string GetNickname()
    {
        return Backend.UserNickName;
    }

    public string GetUID()
    {
        return Backend.UID;
    }
}
