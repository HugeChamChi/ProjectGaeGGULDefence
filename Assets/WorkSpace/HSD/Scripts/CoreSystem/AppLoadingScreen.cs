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
    [Inject] private SceneChangeManager _scenes;
    [Inject] private AppInitialization _initialization;
    [Inject] private IngameTutorialProgress _tutorialProgress;
    private bool _loading;

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
            Show(StartupMessage);
            // Render the small boot cover before loading/activating the battle scene.
            await UniTask.NextFrame(cancellationToken: token);
            if (_tutorialProgress.IsCompleted)
            {
                // Restore the existing guest/linked session and Player before any lobby view binds.
                await _initialization.InitializeAsync(token);
                await _scenes.TransitionToSceneAsync(_lobbyScene).AttachExternalCancellation(token);
                Hide();
            }
            else
            {
                await _scenes.TransitionToSceneAsync(_tutorialScene).AttachExternalCancellation(token);
                // The tutorial releases the cover after its initializer and unit preloads finish.
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Show(_failureMessage);
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
        if (_cover != null) _cover.SetActive(true);
    }

    /// <summary>Reveals the scene after its required preparation has completed.</summary>
    public void Hide()
    {
        if (_cover != null) _cover.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_retryButton != null) _retryButton.onClick.RemoveListener(BeginStartup);
    }
}
