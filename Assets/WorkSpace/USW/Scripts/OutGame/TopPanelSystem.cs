using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BackEnd;

public class TopPanelSystem : MonoBehaviour
{
    [Header("TopPanel UI Elements")]
    [SerializeField] private TextMeshProUGUI _playerNicknameText;
    [SerializeField] private TextMeshProUGUI _playerLevelText;
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _diamondText;
    [SerializeField] private TextMeshProUGUI _staminaText;
    [SerializeField] private TextMeshProUGUI _staminaTimerText;
    [SerializeField] private Image           _expBar;

    [Header("Player Profile")]
    [SerializeField] private GameObject      _playerProfilePopup;
    [SerializeField] private PlayerProfilePopup _profilePopup;
    [SerializeField] private Button          _playerProfileButton;

    private void Start()
    {
        if (_playerNicknameText != null)
            _playerNicknameText.text = Backend.UserNickName ?? "유저";

        if (User.Data != null)
        {
            User.Data.OnUpdateUI             += UpdateTopPanelUI;
            User.Data.OnStaminaRecoveryTimer += UpdateStaminaTimer;

            if (User.Data.Data != null)
                UpdateTopPanelUI(User.Data.Data);
        }

        if (_playerProfileButton != null)
            _playerProfileButton.onClick.AddListener(ShowPlayerProfilePopup);
    }

    private void OnDestroy()
    {
        if (User.Data != null)
        {
            User.Data.OnUpdateUI             -= UpdateTopPanelUI;
            User.Data.OnStaminaRecoveryTimer -= UpdateStaminaTimer;
        }

        if (_playerProfileButton != null)
            _playerProfileButton.onClick.RemoveListener(ShowPlayerProfilePopup);
    }

    public void UpdateTopPanelUI(PlayerData playerData)
    {
        if (playerData == null) return;

        if (_playerLevelText != null)
            _playerLevelText.text = playerData.PlayerLevel.ToString();

        if (_goldText != null)
            _goldText.text = playerData.Gold.ToString("N0");

        if (_diamondText != null)
            _diamondText.text = playerData.Diamond.ToString("N0");

        if (_staminaText != null)
            _staminaText.text = $"{playerData.Stamina}/{playerData.MaxStamina}";

        if (_expBar != null)
        {
            int maxExp = playerData.MaxExp > 0 ? playerData.MaxExp : 500;
            _expBar.fillAmount = Mathf.Clamp01((float)playerData.PlayerExp / maxExp);
        }
    }

    public void UpdateStaminaTimer(int remainingSeconds)
    {
        if (_staminaTimerText == null) return;

        if (remainingSeconds <= 0)
        {
            _staminaTimerText.text = "FULL";
            return;
        }

        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;
        _staminaTimerText.text = $"{minutes:D2}:{seconds:D2}";
    }

    private void ShowPlayerProfilePopup()
    {
        if (_playerProfilePopup == null || _profilePopup == null || User.Data == null)
            return;

        _profilePopup.Init(User.Data.Data);
        _profilePopup.EnableDataBind(true);
        _playerProfilePopup.SetActive(true);
    }
}
