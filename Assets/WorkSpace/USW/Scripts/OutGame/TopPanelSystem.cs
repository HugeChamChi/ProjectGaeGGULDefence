using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BackEnd;
using Cysharp.Threading.Tasks;

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

        if (Player.PlayerData != null)
        {
            Player.PlayerData.OnUpdateUI             += UpdateTopPanelUI;
            Player.PlayerData.OnStaminaRecoveryTimer += UpdateStaminaTimer;

            if (Player.PlayerData.Data != null)
                UpdateTopPanelUI(Player.PlayerData.Data);
        }

        if (_playerProfileButton != null)
            _playerProfileButton.onClick.AddListener(ShowPlayerProfilePopup);
    }

    private void OnDestroy()
    {
        if (Player.PlayerData != null)
        {
            Player.PlayerData.OnUpdateUI             -= UpdateTopPanelUI;
            Player.PlayerData.OnStaminaRecoveryTimer -= UpdateStaminaTimer;
        }

        if (_playerProfileButton != null)
            _playerProfileButton.onClick.RemoveListener(ShowPlayerProfilePopup);
    }

    public void UpdateTopPanelUI(PlayerData playerData)
    {
        if (_playerNicknameText != null)
            _playerNicknameText.text = playerData == null ? "—" : string.IsNullOrWhiteSpace(playerData.PlayerName) ? "플레이어" : playerData.PlayerName;
        if (playerData == null)
        {
            if (_playerLevelText != null) _playerLevelText.text = "—";
            if (_expBar != null) _expBar.fillAmount = 0f;
            if (_goldText != null) _goldText.text = "—";
            if (_diamondText != null) _diamondText.text = "—";
            if (_staminaText != null) _staminaText.text = "—";
            return;
        }

        if (_playerLevelText != null)
            _playerLevelText.text = playerData.PlayerLevel.ToString();

        if (_goldText != null)
            _goldText.text = playerData.Gold.ToString("N0");

        if (_diamondText != null)
            _diamondText.text = playerData.Diamond.ToString("N0");

        if (_staminaText != null)
            _staminaText.text = $"{FormatStaminaAmount(playerData.Stamina)}/{FormatStaminaAmount(playerData.MaxStamina)}";

        if (_expBar != null)
        {
            _expBar.fillAmount = playerData.MaxExp <= 0 ? 1f : Mathf.Clamp01((float)playerData.PlayerExp / playerData.MaxExp);
        }
    }

    public void UpdateStaminaTimer(int remainingSeconds)
    {
        if (_staminaTimerText == null) return;

        if (remainingSeconds <= 0)
        {
            _staminaTimerText.gameObject.SetActive(false);
            return;
        }

        _staminaTimerText.gameObject.SetActive(true);
        int minutes = remainingSeconds / 60;
        int seconds = remainingSeconds % 60;
        _staminaTimerText.text = $"{minutes:D2}:{seconds:D2}";
    }

    private static string FormatStaminaAmount(int amount)
    {
        if (amount >= 1000000000) return (amount / 1000000000d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "B";
        if (amount >= 1000000) return (amount / 1000000d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "M";
        return amount.ToString();
    }

    private void ShowPlayerProfilePopup()
    {
        if (_playerProfilePopup == null || _profilePopup == null || Player.PlayerData == null)
            return;

        _profilePopup.Init(Player.PlayerData.Data);
        _profilePopup.EnableDataBind(true);
        _playerProfilePopup.SetActive(true);
    }
    private void OnApplicationPause(bool paused)
    {
        if (paused) Player.UpdateDirtyDataAsync().Forget();
        else Player.PlayerData.ResumeAsync().Forget();
    }
}
