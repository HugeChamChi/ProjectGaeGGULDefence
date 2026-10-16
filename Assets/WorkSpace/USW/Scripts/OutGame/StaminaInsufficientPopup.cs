using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class StaminaInsufficientPopup : MonoBehaviour
{
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

    private Tween _visibilityTween;

    private void Awake()
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
        _visibilityTween?.Kill();
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
        gameObject.SetActive(true);
        RefreshStatusMessage();
        Open();
    }

    private void RefreshStatusMessage()
    {
        if (_messageText == null) return;
        if (Player.PlayerData?.Data == null) { _messageText.text = "스태미나 정보를 불러오는 중입니다."; return; }
        _messageText.text = $"스태미나\n{Player.PlayerData.Data.Stamina} / {Player.PlayerData.Data.MaxStamina}";
    }

    private void Open()
    {
        gameObject.SetActive(true);
        _visibilityTween?.Kill();
        _popupPanel.SetActive(true);
        _panelTransform.localScale = Vector3.zero;
        _visibilityTween = _panelTransform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void Hide()
    {
        _visibilityTween?.Kill();
        _visibilityTween = _panelTransform.DOScale(Vector3.zero, 0.2f).SetUpdate(true)
            .SetEase(Ease.InBack)
            .OnComplete(() => _popupPanel.SetActive(false));
    }

    private void OnRefillClicked()
    {
        RefillAsync().Forget();
    }
    private bool _refilling;
    private async UniTaskVoid RefillAsync()
    {
        if (_refilling || _staminaConfig == null) return;
        _refilling = true;
        if (_refillButton != null) _refillButton.interactable = false;
        try
        {
            var player = Player.PlayerData;
            // A retry confirms the previous snapshot; it must not charge for another refill.
            bool retry = player.IsDirty;
            if (!retry && !player.TryRefillStamina(_staminaConfig))
            { ShowMessage(player.IsReady ? "다이아가 부족하거나 충전할 수 없습니다." : player.SaveStatus); return; }
            bool saved = await player.TrySaveAsync();
            if (this == null) return;
            if (saved) RefreshStatusMessage(); else ShowMessage(player.SaveStatus + "\n다시 누르면 저장만 재시도합니다.");
        }
        finally { _refilling = false; if (this != null && _refillButton != null) _refillButton.interactable = true; }
    }
    /// <summary>Displays a recoverable loading/save/entry failure through the existing popup.</summary>
    public void ShowMessage(string message) { if (_messageText != null) _messageText.text = message; Open(); }
}
