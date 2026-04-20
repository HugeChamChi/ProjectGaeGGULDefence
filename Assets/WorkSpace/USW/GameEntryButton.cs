using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameEntryButton : MonoBehaviour
{
    [SerializeField] private PlayerDataController _playerDataController;
    [SerializeField] private Button _enterButton;
    [SerializeField] private string _targetSceneName = "InGameScene";
    [SerializeField] private StaminaInsufficientPopup _staminaInsufficientPopup;

    private const int STAMINA_COST = 5;

    private void Start()
    {
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
        if (_playerDataController == null)
        {
            Debug.LogError("PlayerDataController가 연결되지 않았습니다.");
            return;
        }

        bool success = _playerDataController.UseStamina(STAMINA_COST);

        if (success)
        {
            Debug.Log($"스태미나 {STAMINA_COST} 소모 → {_targetSceneName} 입장");
            SceneManager.LoadScene(_targetSceneName);
        }
        else
        {
            Debug.LogWarning($"스태미나 부족! 현재: {_playerDataController.Data.Stamina}, 필요: {STAMINA_COST}");
            _staminaInsufficientPopup?.Show(_playerDataController.Data.Stamina, STAMINA_COST);
        }
    }
}
