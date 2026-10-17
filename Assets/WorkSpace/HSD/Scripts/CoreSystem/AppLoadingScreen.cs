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
    private float _progress;
    private float _reportedProgress;

    private void Awake()
    {
        if (_retryButton != null) _retryButton.onClick.AddListener(BeginStartup);
        if (SceneManager.GetActiveScene().name == BootSceneName) Show(StartupMessage);
        else Hide();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == BootSceneName) BeginStartup();
    }

    private string StartupMessage => _tutorialProgress != null && _tutorialProgress.IsCompleted ? _returningMessage : _startupMessage;

    private void BeginStartup() => LoadStartupAsync().Forget();

    private async UniTask LoadStartupAsync()
    {
        if (_loading) return;
        _loading = true;
        var token = this.GetCancellationTokenOnDestroy();
        try
        {
            ResetProgress();
            Show(StartupMessage);
            // Render the small boot cover before loading/activating the battle scene.
            await UniTask.NextFrame(cancellationToken: token);
            if (_tutorialProgress.IsCompleted)
            {
                // Restore the existing guest/linked session and Player before any lobby view binds.
                await _initialization.InitializeAsync(token);
                ReportProgress(_initializedProgress);
                await _scenes.TransitionToSceneAsync(_lobbyScene).AttachExternalCancellation(token);
                Hide();
            }
            else
            {
                await _scenes.TransitionToSceneAsync(_tutorialScene).AttachExternalCancellation(token);
                ReportProgress(_sceneLoadedProgress);
                // The tutorial releases the cover after its initializer and unit preloads finish.
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Show(_failureMessage);
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
