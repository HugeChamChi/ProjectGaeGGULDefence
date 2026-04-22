using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class StaminaInsufficientPopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _backgroundButton;

    private void Start()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_backgroundButton != null)
            _backgroundButton.onClick.AddListener(Hide);

        _popupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);

        if (_backgroundButton != null)
            _backgroundButton.onClick.RemoveListener(Hide);
    }

    public void Show(int currentStamina, int requiredStamina)
    {
        if (_messageText != null)
            _messageText.text = $"스태미나가 부족합니다!\n현재: {currentStamina} / 필요: {requiredStamina}";

        _popupPanel.SetActive(true);
        _popupPanel.transform.localScale = Vector3.zero;
        _popupPanel.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
    }

    public void Hide()
    {
        _popupPanel.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => _popupPanel.SetActive(false));
    }
}
