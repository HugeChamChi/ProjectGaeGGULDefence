using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine;

public class UI_ProfileChangePanel : UI_ProfilePanelBase
{
    [Header("Components")]
    [SerializeField] Button changeButton;
    [SerializeField] Button profileButton;
    [SerializeField] Button frameButton;
    [SerializeField] TextMeshProUGUI unlockDescription;
    IProfileItem currentIconData;
    Sprite currentIcon;

    [Header("Button Color")]
    [SerializeField] Color selectColor;
    [SerializeField] Color unselectColor;

    private void OnEnable()
    {
        SelectProfile();

        profileButton.onClick.AddListener(SelectProfile);
        frameButton.onClick.AddListener(SelectFrame);
    }

    private void OnDisable()
    {
        profileButton.onClick.RemoveListener(SelectProfile);
        frameButton.onClick.RemoveListener(SelectFrame);
    }

    public void Setup(Sprite icon)
    {
        currentIcon = icon;
        ui_ProfileIconSlot.Setup(currentIcon, null);
    }

    public void SelectIcon(IProfileItem iconData)
    {
        currentIconData = iconData;
        ui_ProfileIconSlot.Setup(iconData.Sprite, null);
        unlockDescription.text = iconData.UnlockDescription;

        changeButton.interactable = CanChangeIcon();
    }

    private void SelectProfile()
    {
        profileButton.image.color = selectColor;
        frameButton.image.color = unselectColor;
    }

    private void SelectFrame()
    {
        profileButton.image.color = unselectColor;
        frameButton.image.color = selectColor;
    }

    private bool CanChangeIcon()
    {
        return currentIconData != null && currentIconData.Sprite != currentIcon && currentIconData.IsUnlocked;
    }
}

public class UI_ProfileSlotController : MonoBehaviour
{
    [SerializeField] ScrollRect scroll;

    public void Setup(PlayerData playerData)
    {
        
    }
}