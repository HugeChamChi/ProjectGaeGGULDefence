using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>본게임 진입과 로비 강화 탭 선택을 연결한다.</summary>
public sealed class LobbyFeatureNavigation : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("_tutorialButton")]
    [SerializeField] private Button _ingameButton;
    [SerializeField] private Button _researchButton;
    [SerializeField] private BottomPanelController _lobbyTabs;
    [Inject] private SceneChangeManager _scenes;
    private bool _moving;
    private void Start()
    {
        if (_ingameButton != null) _ingameButton.onClick.AddListener(OpenIngame);
        if (_researchButton != null) _researchButton.onClick.AddListener(OpenResearch);
    }
    private void OnDestroy()
    {
        if (_ingameButton != null) _ingameButton.onClick.RemoveListener(OpenIngame);
        if (_researchButton != null) _researchButton.onClick.RemoveListener(OpenResearch);
    }
    /// <summary>튜토리얼 완료 여부와 관계없이 본게임으로 진입한다.</summary>
    public void OpenIngame() => OpenAsync("IngameScene").Forget();
    /// <summary>로비의 강화 패널을 바로 선택한다.</summary>
    public void OpenResearch() => _lobbyTabs?.SelectButton(3);
    private async UniTask OpenAsync(string scene)
    {
        if (_moving) return;
        _moving = true;
        try { await _scenes.TransitionToSceneAsync(scene); }
        catch (System.Exception error) { Debug.LogWarning("화면을 열지 못했습니다: " + error.Message); }
        finally { _moving = false; }
    }
}
