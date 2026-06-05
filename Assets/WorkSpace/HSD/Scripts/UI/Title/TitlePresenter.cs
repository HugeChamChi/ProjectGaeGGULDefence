using UnityEngine;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using BackEnd;

public class TitlePresenter : IInitializable, ITickable, IAsyncStartable
{
    private readonly GlobalUIManager _uiManager;
    private readonly SceneChangeManager _sceneChangeManager;
    private readonly GameDataManager _gameDataManager;
    private readonly BackendGameData _backendData;
    private readonly TitleView _view;

    private bool _isProcessing = false;

    [Inject]
    public TitlePresenter(GlobalUIManager uiManager, SceneChangeManager sceneChangeManager, GameDataManager gameDataManager, BackendGameData backendData, TitleView view = null)
    {
        _uiManager = uiManager;
        _sceneChangeManager = sceneChangeManager;
        _gameDataManager = gameDataManager;
        _backendData = backendData;
        _view = view;

        // Player 및 하위 컨트롤러에 의존성 주입 전달
        Player.Inject(_backendData);
    }

    public void Initialize()
    {
        if (_view != null && _view.startButton != null)
        {
            _view.startButton.onClick.AddListener(OnStartButtonClicked);
        }
    }

    private void OnStartButtonClicked()
    {
        if (_isProcessing) return;
        _isProcessing = true;
        ProcessInitializationAsync().Forget();
    }

    public async System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellation)
    {
        if (_uiManager != null)
        {
            await _uiManager.FadeOutAsync();
        }
    }

    public void Tick()
    {
        if (_isProcessing) return;

        // 버튼이 명시적으로 연결되지 않았을 때만 화면 전체 터치 작동
        if (_view == null || _view.startButton == null)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                OnStartButtonClicked();
            }
        }
    }

    private async UniTask ProcessInitializationAsync()
    {
        if (_sceneChangeManager != null)
        {
            // SceneChangeManager의 완벽한 흐름(FadeIn -> beforeLoad -> Load -> afterLoad -> FadeOut)에 올라탑니다.
            await _sceneChangeManager.TransitionToSceneAsync("LobbyScene", 
                beforeLoad: async () =>
                {
                    // 1. 씬이 넘어가기 전 (FadeIn으로 화면이 가려진 상태)에서 백엔드 및 각종 데이터 초기화
                    var initBro = Backend.Initialize();
                    if (initBro.IsSuccess())
                    {
                        Debug.Log("Backend Init Success");
                        
                        // 구글 해시 키 출력 (로그캣 확인용)
                        string googleHash = Backend.Utils.GetGoogleHash();
                        Debug.Log($"[Backend] 현재 빌드의 구글 해시 키(Google Hash): {googleHash}");

                        var loginBro = Backend.BMember.CustomLogin("test", "test");
                        if (!loginBro.IsSuccess())
                        {
                            var signUpBro = Backend.BMember.CustomSignUp("test", "test");
                            if (signUpBro.IsSuccess())
                            {
                                Backend.BMember.CustomLogin("test", "test");
                            }
                            else
                            {
                                Debug.LogError($"Backend Sign Up Failed: {signUpBro}");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Backend SDK Init Failed or Not Present. Mock continuing.");
                    }

                    // 2. Table Initialize, Data Parsing
                    if (_gameDataManager != null)
                    {
                        await _gameDataManager.LoadAllAsync();
                    }

                    // 3. Player Initialize
                    await Player.InitializeAsync();
                }
            );
        }
    }
}
