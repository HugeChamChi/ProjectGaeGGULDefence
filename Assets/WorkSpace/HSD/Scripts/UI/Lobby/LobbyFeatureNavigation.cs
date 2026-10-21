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
    [Inject] private UI_PartySelectView _partySelect;
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
    /// <summary>공용 모드 선택과 스태미나 출격 절차를 연다.</summary>
    public void OpenIngame() => _partySelect?.StartDefaultGame();
    /// <summary>로비의 강화 패널을 바로 선택한다.</summary>
    public void OpenResearch() => _lobbyTabs?.SelectButton(3);
}
