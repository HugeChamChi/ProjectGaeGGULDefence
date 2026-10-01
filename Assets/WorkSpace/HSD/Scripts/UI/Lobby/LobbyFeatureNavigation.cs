using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>로비에서 전용 튜토리얼과 영구 강화 화면으로 이동한다.</summary>
public sealed class LobbyFeatureNavigation : MonoBehaviour
{
    [SerializeField] private Button _tutorialButton;
    [SerializeField] private Button _researchButton;
    [Inject] private SceneChangeManager _scenes;
    private bool _moving;
    private void Start()
    {
        _tutorialButton.onClick.AddListener(OpenTutorial);
        _researchButton.onClick.AddListener(OpenResearch);
    }
    /// <summary>완료 여부와 관계없이 다시 배울 수 있다.</summary>
    public void OpenTutorial() => OpenAsync("TutorialScene").Forget();
    /// <summary>기기에 저장된 무료 영구 강화 화면을 연다.</summary>
    public void OpenResearch() => OpenAsync("OutgameUpgrade").Forget();
    private async UniTask OpenAsync(string scene)
    {
        if (_moving) return;
        _moving = true;
        try { await _scenes.TransitionToSceneAsync(scene); }
        catch (System.Exception error) { Debug.LogWarning("화면을 열지 못했습니다: " + error.Message); }
        finally { _moving = false; }
    }
}
