using UnityEngine;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using BackEnd;

public class TitlePresenter : IInitializable, ITickable, IAsyncStartable, System.IDisposable
{
    private readonly GlobalUIManager _uiManager;
    private readonly SceneChangeManager _sceneChangeManager;
    private readonly AppInitialization _initialization;
    private System.Threading.CancellationToken _cancellation;
    private readonly TitleView _view;

    private bool _isProcessing = false;

    [Inject]
    public TitlePresenter(GlobalUIManager uiManager, SceneChangeManager sceneChangeManager, AppInitialization initialization, TitleView view = null)
    {
        _uiManager = uiManager;
        _sceneChangeManager = sceneChangeManager;
        _initialization = initialization;
        _view = view;

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

    public async Awaitable StartAsync(System.Threading.CancellationToken cancellation)
    {
        _cancellation = cancellation;
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
        try
        {
            _view?.SetStatus("로그인 중…");
            await _sceneChangeManager.TransitionToSceneAsync("LobbyScene",
                beforeLoad: () => _initialization.InitializeAsync(_cancellation));
        }
        catch (System.OperationCanceledException) { }
        catch (System.Exception error)
        {
            Debug.LogWarning("[Title] 초기화 실패: " + error.Message);
            if (_view != null) _view.SetStatus("연결에 실패했습니다. 화면을 눌러 다시 시도해 주세요.");
        }
        finally { _isProcessing = false; }
    }

    /// <summary>타이틀 씬이 닫힐 때 버튼 구독을 해제한다.</summary>
    public void Dispose()
    {
        if (_view != null && _view.startButton != null)
            _view.startButton.onClick.RemoveListener(OnStartButtonClicked);
    }
}
