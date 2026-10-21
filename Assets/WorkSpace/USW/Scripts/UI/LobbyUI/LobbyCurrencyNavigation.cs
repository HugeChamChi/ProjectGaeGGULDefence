using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-bound touch targets for stamina status and currency shop navigation.</summary>
public sealed class LobbyCurrencyNavigation : MonoBehaviour
{
    [SerializeField] private Button _staminaButton;
    [SerializeField] private Button _goldButton;
    [SerializeField] private Button _diamondButton;
    [SerializeField] private StaminaInsufficientPopup _staminaPopup;
    [SerializeField] private BottomPanelController _tabs;
    [SerializeField] private TMP_Text _shopStatus;
    private const int ShopTab = 0;

    private void Awake()
    {
        _staminaButton?.onClick.AddListener(OpenStamina);
        _goldButton?.onClick.AddListener(OpenGold);
        _diamondButton?.onClick.AddListener(OpenDiamond);
    }
    private void OpenStamina() => _staminaPopup?.ShowStatus();
    private void OpenGold() => OpenShop("Gold");
    private void OpenDiamond() => OpenShop("Dia");
    private void OpenShop(string currency)
    {
        _tabs?.SelectButton(ShopTab);
        // The current shop page is a placeholder. No prices, products or rewards are invented here.
        if (_shopStatus != null) _shopStatus.text = currency + " 상품 준비 중";
    }
    private void OnDestroy()
    {
        if (_staminaButton != null) _staminaButton.onClick.RemoveListener(OpenStamina);
        if (_goldButton != null) _goldButton.onClick.RemoveListener(OpenGold);
        if (_diamondButton != null) _diamondButton.onClick.RemoveListener(OpenDiamond);
    }
}
