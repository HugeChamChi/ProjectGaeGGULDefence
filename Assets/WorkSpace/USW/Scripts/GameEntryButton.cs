using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameEntryButton : MonoBehaviour
{
    [SerializeField] private Button _enterButton;
    [SerializeField] private string _targetSceneName = "InGameScene";
    [SerializeField] private StaminaInsufficientPopup _staminaInsufficientPopup;

    private const int DEFAULT_STAMINA_COST = 5;
    private int StaminaCost
    {
        get
        {
            var config = RM.Load<StaminaConfig>("Data/StaminaConfig");
            return config != null ? config.stageEntryCost : DEFAULT_STAMINA_COST;
        }
    }

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
        if (Player.PlayerData == null)
        {
            Debug.LogError("PlayerDataController가 연결되지 않았습니다.");
            return;
        }

        int cost = StaminaCost;
        bool success = Player.PlayerData.UseStamina(cost);

        if (success)
        {
            Debug.Log($"스태미나 {cost} 소모 → {_targetSceneName} 입장");
            SceneManager.LoadScene(_targetSceneName);
        }
        else
        {
            Debug.LogWarning($"스태미나 부족! 현재: {Player.PlayerData.Data.Stamina}, 필요: {cost}");
            _staminaInsufficientPopup?.Show(Player.PlayerData.Data.Stamina, cost);
        }
    }
}
