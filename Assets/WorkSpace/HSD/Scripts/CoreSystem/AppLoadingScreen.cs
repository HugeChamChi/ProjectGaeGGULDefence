using System;
using Cysharp.Threading.Tasks;
using GaeGGUL.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

/// <summary>Root-owned loading cover for the lightweight boot scene and tutorial-to-lobby preparation.</summary>
public sealed class AppLoadingScreen : MonoBehaviour
{
    /// <summary>The lightweight Player entry scene, before any battle resources are loaded.</summary>
    public const string BootSceneName = "BootScene";
    [SerializeField] private GameObject _cover;
    [SerializeField] private TMP_Text _message;
    [SerializeField] private Button _retryButton;
    [SerializeField] private string _tutorialScene = "TutorialScene";
    [SerializeField] private string _lobbyScene = "LobbyScene";
    [SerializeField] private string _loginMessage = "계정 확인 중…";
    [SerializeField] private string _startupMessage = "튜토리얼 준비 중…";
    [SerializeField] private string _returningMessage = "로비 준비 중…";
    [SerializeField] private string _failureMessage = "불러오지 못했어요. 다시 시도해 주세요.";
    [Header("Progress Bar")]
    [SerializeField] private GameObject _progressBar;
    [SerializeField] private RectTransform _progressFill;
    [Tooltip("Share of the remaining gap the bar creeps toward until the next stage reports.")]
    [SerializeField, Range(0f, 1f)] private float _progressCreepCap = 0.9f;
    [Tooltip("Exponential easing rate (per second) toward the creep goal.")]
    [SerializeField] private float _progressEaseRate = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _initializedProgress = 0.5f;
    [SerializeField, Range(0f, 1f)] private float _sceneLoadedProgress = 0.85f;
    [Inject] private SceneChangeManager _scenes;
    [Inject] private AppInitialization _initialization;
    [Inject] private IngameTutorialProgress _tutorialProgress;
    private bool _loading;
    private bool _shopTest;
    private float _progress;
    private float _reportedProgress;

    private void Awake()
    {
        _shopTest = SceneManager.GetActiveScene().name == "ShopTestTitle";
        if (_shopTest)
        {
            _lobbyScene = "ShopTestLobby";
            _startupMessage = _returningMessage = "상점 테스트 준비 중…";
            foreach (var image in _cover.GetComponentsInChildren<Image>(true))
                if (image.name == "LobbyBackground")
                { image.sprite = null; image.color = new Color(.18f, .06f, .32f, 1f); }
            var title = new GameObject("ShopTestBuildLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.transform.SetParent(_cover.transform, false);
            var text = title.GetComponent<TextMeshProUGUI>();
            text.font = _message.font;
            text.text = "SHOP TEST\n" + Application.version;
            text.fontSize = 56;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var rect = title.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.1f, .7f);
            rect.anchorMax = new Vector2(.9f, .9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        if (_retryButton != null) _retryButton.onClick.AddListener(BeginStartup);
        if (_shopTest || SceneManager.GetActiveScene().name == BootSceneName) Show(_loginMessage);
        else Hide();
    }

    private void Start()
    {
        if (_shopTest || SceneManager.GetActiveScene().name == BootSceneName) BeginStartup();
    }

    private void BeginStartup() => LoadStartupAsync().Forget();

    private async UniTask LoadStartupAsync()
    {
        if (_loading) return;
        _loading = true;
        var token = this.GetCancellationTokenOnDestroy();
        try
        {
            ResetProgress();
            Show(_loginMessage);
            // Render the small boot cover before loading/activating the battle scene.
            await UniTask.NextFrame(cancellationToken: token);
            // Restore the existing guest/linked account before consulting its tutorial record.
            // A device-only legacy flag cannot establish completion for the authenticated owner.
            await _initialization.InitializeAsync(token);
            ReportProgress(_initializedProgress);
            if (_shopTest || _tutorialProgress.IsCompleted)
            {
                Show(_returningMessage);
                await _scenes.TransitionToSceneAsync(_lobbyScene, afterLoad: () =>
                {
                    ReportProgress(1f);
                    Hide();
                    return UniTask.CompletedTask;
                }).AttachExternalCancellation(token);
            }
            else
            {
                Show(_startupMessage);
                await _scenes.TransitionToSceneAsync(_tutorialScene, afterLoad: async () =>
                {
                    ReportProgress(_sceneLoadedProgress);
                    var tutorial = FindFirstObjectByType<IngameTutorialDirector>();
                    if (tutorial == null) throw new InvalidOperationException("Tutorial director missing.");
                    await tutorial.EntryPreparation.AttachExternalCancellation(token);
                    ReportProgress(1f);
                    // Remove the boot cover while the persistent transition is still opaque.
                    Hide();
                }).AttachExternalCancellation(token);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Show(_shopTest ? "상점 테스트 로딩 실패\n" + error.Message : _failureMessage);
            if (_progressBar != null) _progressBar.SetActive(false);
            if (_retryButton != null) _retryButton.gameObject.SetActive(true);
            Debug.LogWarning("[AppLoadingScreen] Startup preparation failed: " + error.Message);
        }
        finally { _loading = false; }
    }

    /// <summary>Shows an opaque, input-blocking cover above scene UI while preparation runs.</summary>
    public void Show(string message)
    {
        if (_message != null) _message.text = message;
        if (_retryButton != null) _retryButton.gameObject.SetActive(false);
        if (_progressBar != null) _progressBar.SetActive(true);
        if (_cover != null) _cover.SetActive(true);
    }

    /// <summary>Raises the bar's floor to a completed preparation stage (0-1); it never moves backwards.</summary>
    public void ReportProgress(float progress)
    {
        _reportedProgress = Mathf.Max(_reportedProgress, Mathf.Clamp01(progress));
    }

    /// <summary>Reveals the scene after its required preparation has completed.</summary>
    public void Hide()
    {
        if (_cover != null) _cover.SetActive(false);
    }

    private void Update()
    {
        if (_progressFill == null || _cover == null || !_cover.activeSelf) return;
        // Ease toward a goal short of the next stage so the bar keeps moving without claiming completion.
        float goal = _reportedProgress + (1f - _reportedProgress) * _progressCreepCap;
        _progress += (goal - _progress) * (1f - Mathf.Exp(-_progressEaseRate * Time.unscaledDeltaTime));
        ApplyProgress();
    }

    private void ResetProgress()
    {
        _progress = 0f;
        _reportedProgress = 0f;
        ApplyProgress();
    }

    private void ApplyProgress()
    {
        if (_progressFill != null) _progressFill.anchorMax = new Vector2(_progress, 1f);
    }

    private void OnDestroy()
    {
        if (_retryButton != null) _retryButton.onClick.RemoveListener(BeginStartup);
    }
}
