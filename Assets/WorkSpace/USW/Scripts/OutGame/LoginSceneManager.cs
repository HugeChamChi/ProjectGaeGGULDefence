using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using BackEnd;
using Cysharp.Threading.Tasks;
using System.Threading;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

public class LoginSceneManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image loadingBarFill;

    [Header("설정")]
    [SerializeField] private float loadingFillDuration = 0.5f;

    private bool _isLoggingIn = false;
    private bool _loginDone   = false;
    private CancellationTokenSource _loadingBarCts;

    private void Start()
    {
        loadingBarFill.fillAmount = 0f;
        InitBackend();
    }

    /// <summary>
    /// 1. 뒤끝 초기화
    /// </summary>
    private void InitBackend()
    {
        var bro = Backend.Initialize();

        if (bro.IsSuccess())
        {
            Debug.Log("뒤끝 초기화 성공");
            InitGPGS();
        }
        else
        {
            Debug.LogError("뒤끝 초기화 실패 : " + bro);
        }
    }

    /// <summary>
    /// 2. GPGS 초기화
    /// </summary>
    private void InitGPGS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayGamesPlatform.DebugLogEnabled = true;
        PlayGamesPlatform.Activate();
#endif
        Debug.Log("GPGS 초기화 완료");
    }

    /// <summary>
    /// 3. 터치 감지
    /// </summary>
    private void Update()
    {
        if (_isLoggingIn) return;

        if (Input.GetMouseButtonDown(0) ||
            (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            _isLoggingIn = true;
            StartLogin();
        }
    }

    /// <summary>
    /// 에디터/실사용 로그인 분기점
    /// </summary>
    private void StartLogin()
    {
        _loginDone                = false;
        loadingBarFill.fillAmount = 0f;

        _loadingBarCts?.Cancel();
        _loadingBarCts?.Dispose();
        // OnLoginFailed()에서 수동 취소 가능하고,
        // 오브젝트 Destroy 시에도 자동 취소되도록 DestroyToken과 연결
        _loadingBarCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        LoadingBarAsync(_loadingBarCts.Token).Forget(Debug.LogException);

#if UNITY_EDITOR
        EditorLogin();
#else
        GoogleLogin();
#endif
    }

    /// <summary>
    /// 에디터 전용 로그인
    /// </summary>
    private void EditorLogin()
    {
        var bro = Backend.BMember.CustomLogin("testuser", "testpass");

        if (bro.IsSuccess())
        {
            Debug.Log("에디터 로그인 성공");
            _loginDone = true;
        }
        else
        {
            bro = Backend.BMember.CustomSignUp("testuser", "testpass");

            if (bro.IsSuccess())
            {
                Debug.Log("에디터 회원가입 + 로그인 성공");
                _loginDone = true;
            }
            else
            {
                Debug.LogError("에디터 로그인 실패 : " + bro);
                OnLoginFailed();
            }
        }
    }

    /// <summary>
    /// 실기기 Google 로그인
    /// </summary>
    private void GoogleLogin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        PlayGamesPlatform.Instance.Authenticate((status) =>
        {
            if (status == SignInStatus.Success)
            {
                Debug.Log("Google 로그인 성공");

                PlayGamesPlatform.Instance.RequestServerSideAccess(true, (authCode) =>
                {
                    if (string.IsNullOrEmpty(authCode))
                    {
                        Debug.LogError("AuthCode 획득 실패");
                        OnLoginFailed();
                        return;
                    }

                    BackendGoogleLogin(authCode);
                });
            }
            else
            {
                Debug.LogError("Google 로그인 실패 : " + status);
                OnLoginFailed();
            }
        });
#else
        Debug.LogWarning("GoogleLogin: GPGS 플러그인 없음 — 에디터에서는 EditorLogin 사용");
        OnLoginFailed();
#endif
    }

    /// <summary>
    /// 뒤끝 Google Federation 로그인
    /// </summary>
    private void BackendGoogleLogin(string authCode)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var bro = Backend.BMember.AuthorizeFederation(authCode, FederationType.Google);

        if (bro.IsSuccess())
        {
            Debug.Log("뒤끝 로그인 성공");
            _loginDone = true;
        }
        else
        {
            Debug.LogError("뒤끝 로그인 실패 : " + bro);
            OnLoginFailed();
        }
#endif
    }

    private async UniTask LoadingBarAsync(CancellationToken token)
    {
        try
        {
            float elapsed  = 0f;
            float duration = 2f;

            // Phase 1: 0 → 0.99
            while (loadingBarFill.fillAmount < 0.99f)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                loadingBarFill.fillAmount = Mathf.Clamp01(elapsed / duration * 0.99f);
                await UniTask.Yield(token);
            }

            // Phase 2: 로그인 완료 대기 — 실패 시 token 취소로 탈출
            await UniTask.WaitUntil(() => _loginDone, cancellationToken: token);

            // Phase 3: 0.99 → 1.0
            elapsed = 0f;
            float startFill = loadingBarFill.fillAmount;

            while (loadingBarFill.fillAmount < 1f)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                loadingBarFill.fillAmount = Mathf.Lerp(startFill, 1f, elapsed / loadingFillDuration);
                await UniTask.Yield(token);
            }

            loadingBarFill.fillAmount = 1f;
            await UniTask.Delay(300, cancellationToken: token);

            SceneManager.LoadScene("LobbyScene");
        }
        catch (OperationCanceledException) { }
    }

    private void OnLoginFailed()
    {
        _loadingBarCts?.Cancel();
        _loadingBarCts?.Dispose();
        _loadingBarCts = null;

        _isLoggingIn              = false;
        _loginDone                = false;
        loadingBarFill.fillAmount = 0f;
        Debug.LogError("로그인 실패 - 다시 터치해주세요");
    }
}
