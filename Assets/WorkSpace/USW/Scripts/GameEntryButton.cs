using UnityEngine;
using UnityEngine.UI;

public class GameEntryButton : MonoBehaviour
{
    [SerializeField] private Button _enterButton;
    [SerializeField] private string _targetSceneName = "InGameScene";
    [SerializeField] private StaminaInsufficientPopup _staminaInsufficientPopup;
    [SerializeField] private UI_PartySelectView _partySelectView;

    private void Start()
    {
        // Route old serialized entry buttons through the same remote preflight and stamina reservation path.
        if (_partySelectView == null)
            _partySelectView = GetComponentInParent<Canvas>()?.GetComponentInChildren<UI_PartySelectView>(true);
        if (_enterButton != null)
            _enterButton.onClick.AddListener(OnEnterButtonClicked);
    }

    private void OnDestroy()
    {
        if (_enterButton != null)
            _enterButton.onClick.RemoveListener(OnEnterButtonClicked);
    }

    private void OnEnterButtonClicked()
    {
        if (_partySelectView == null)
        {
            _staminaInsufficientPopup?.ShowMessage("출격 화면을 준비하지 못했습니다.");
            return;
        }
        _partySelectView.StartDefaultGame();
    }
}
