using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class StaminaInsufficientPopup : MonoBehaviour
{
    // PopupManager와 동일한 정적 싱글턴 패턴 - DI 없이도 어디서든 StaminaInsufficientPopup.Instance로 접근 가능
    public static StaminaInsufficientPopup Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private RectTransform _panelTransform;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _backgroundButton;

    [Header("다이아 즉시 회복")]
    [SerializeField] private Button _refillButton;
    [SerializeField] private TextMeshProUGUI _refillButtonText;
    [SerializeField] private StaminaConfig _staminaConfig;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_backgroundButton != null)
            _backgroundButton.onClick.AddListener(Hide);

        if (_refillButton != null)
            _refillButton.onClick.AddListener(OnRefillClicked);

        if (_refillButtonText != null && _staminaConfig != null)
            _refillButtonText.text = $"다이아 {_staminaConfig.diamondCostForRefill}개로 스태미나 +{_staminaConfig.refillAmount}";

        _popupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);

        if (_backgroundButton != null)
            _backgroundButton.onClick.RemoveListener(Hide);

        if (_refillButton != null)
            _refillButton.onClick.RemoveListener(OnRefillClicked);
    }

    /// <summary>
    /// 스태미나 부족으로 진입 실패했을 때 표시 (필요량 대비 안내)
    /// </summary>
    public void Show(int currentStamina, int requiredStamina)
    {
        if (_messageText != null)
            _messageText.text = $"스태미나가 부족합니다!\n현재: {currentStamina} / 필요: {requiredStamina}";

        Open();
    }

    /// <summary>
    /// 에너지 아이콘을 직접 눌러서 현재 상태 확인 + 즉시회복 용도로 열 때 사용
    /// </summary>
    public void ShowStatus()
    {
        RefreshStatusMessage();
        Open();
    }

    private void RefreshStatusMessage()
    {
        if (_messageText == null || Player.PlayerData?.Data == null) return;
        _messageText.text = $"스태미나\n{Player.PlayerData.Data.Stamina} / {Player.PlayerData.Data.MaxStamina}";
    }

    private void Open()
    {
        _popupPanel.SetActive(true);
        _panelTransform.localScale = Vector3.zero;
        _panelTransform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
    }

    public void Hide()
    {
        _panelTransform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => _popupPanel.SetActive(false));
    }

    private void OnRefillClicked()
    {
        if (Player.PlayerData?.Data == null || _staminaConfig == null) return;

        bool success = Player.PlayerData.SpendDiamond(_staminaConfig.diamondCostForRefill);
        if (success)
        {
            Player.PlayerData.AddStamina(_staminaConfig.refillAmount);
            RefreshStatusMessage();
        }
        else if (_messageText != null)
        {
            _messageText.text = "다이아가 부족합니다.";
        }
    }
}
