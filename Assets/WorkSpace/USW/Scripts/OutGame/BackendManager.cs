using UnityEngine;
using BackEnd;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

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

#if UNITY_EDITOR
        AutoLoginForEditorTesting();
#endif
    }

#if UNITY_EDITOR
    // LobbyScene을 타이틀 로그인 없이 바로 열어서 테스트할 때를 위한 에디터 전용 자동 로그인.
    // LoginSceneManager.EditorLogin()과 동일한 테스트 계정을 사용하며, 타이틀 씬을 정상적으로
    // 거쳐올 때는 활성 씬이 LobbyScene이 아니므로 개입하지 않는다.
    private void AutoLoginForEditorTesting()
    {
        if (SceneManager.GetActiveScene().name != "LobbyScene") return;
        if (Backend.IsLogin) return;

        Backend.Initialize();

        var bro = Backend.BMember.CustomLogin("testuser", "testpass");
        if (!bro.IsSuccess())
        {
            bro = Backend.BMember.CustomSignUp("testuser", "testpass");
        }

        if (!bro.IsSuccess())
        {
            Debug.LogError("[BackendManager] 에디터 자동 로그인 실패: " + bro);
            return;
        }

        Debug.Log("[BackendManager] 에디터 자동 로그인 성공 (testuser)");

        // 로그인만으로는 Player.PlayerData가 비어있어 스태미나 등 UI가 갱신되지 않는다.
        // 타이틀 씬의 TitlePresenter가 하는 것과 동일하게 데이터 주입 + 초기화까지 진행한다.
        Player.Inject(new BackendGameData());
        Player.InitializeAsync().Forget(e => Debug.LogException(e));
    }
#endif

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
