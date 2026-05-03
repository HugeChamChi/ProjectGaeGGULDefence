using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ProfileChangePanel : UI_ProfilePanelBase
{
    [Header("Components")]
    [SerializeField] UI_PlayerProfileIconSlot profileSlot;
    [SerializeField] UI_ProfileSlotController iconController;
    [SerializeField] UI_ProfileSlotController frameController;

    [Header("UI")]
    [SerializeField] Button changeButton;
    [SerializeField] Button iconButton;
    [SerializeField] Button frameButton;
    [SerializeField] TextMeshProUGUI unlockDescription;
    IProfileItem currentIconData;
    IProfileItem currentFrameData;

    [Header("Button Color")]
    [SerializeField] Color selectColor;
    [SerializeField] Color unselectColor;

    protected override void OnEnable()
    {
        base.OnEnable();
        // Initial setup from current user data
        RefreshData();

        profileSlot.Setup(currentIconData);
        profileSlot.Setup(currentFrameData);

        iconController.Setup(Select);
        frameController.Setup(Select);

        ChangeIconList();

        iconButton.onClick.AddListener(ChangeIconList);
        frameButton.onClick.AddListener(ChangeFrameList);
        changeButton.onClick.AddListener(OnChangeButtonClick);
    }

    private void RefreshData()
    {
        currentIconData = Player.Profile.CurrentIconData;
        currentFrameData = Player.Profile.CurrentFrameData;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        iconButton.onClick.RemoveListener(ChangeIconList);
        frameButton.onClick.RemoveListener(ChangeFrameList);
        changeButton.onClick.RemoveListener(OnChangeButtonClick);
    }

    public void Select(IProfileItem item)
    {
        if (item.Type == ProfileItemType.Icon)
        {
            currentIconData = item;
        }
        else if (item.Type == ProfileItemType.Frame)
        {
            currentFrameData = item;
        }

        profileSlot.Setup(item);
        UpdateStatus(item);
    }

    private void UpdateStatus(IProfileItem item)
    {
        bool isSelected = (item == currentIconData || item == currentFrameData);
        unlockDescription.text = item.UnlockCondition;

        changeButton.interactable = item.IsUnlocked && !isSelected;
    }

    private void ChangeIconList()
    {
        RefreshData();
        profileSlot.Setup(currentIconData);
        profileSlot.Setup(currentFrameData);

        iconButton.image.color = selectColor;
        frameButton.image.color = unselectColor;

        iconController.gameObject.SetActive(true);
        frameController.gameObject.SetActive(false);
    }

    private void ChangeFrameList()
    {
        // Reset icon preview to equipped one when switching to Frame list
        currentIconData = Player.Profile.CurrentIconData;
        profileSlot.Setup(currentIconData);

        iconButton.image.color = unselectColor;
        frameButton.image.color = selectColor;

        iconController.gameObject.SetActive(false);
        frameController.gameObject.SetActive(true);

        Select(currentFrameData);
    }

    private void OnChangeButtonClick()
    {
        // Apply changes to data
        Player.Profile.Data.CurrentIconId = currentIconData.Id;
        Player.Profile.Data.CurrentFrameId = currentFrameData.Id;

        // Save to backend
        Player.Profile.Save();

        // Refresh slot lists to show new "Equipped" state
        iconController.Refresh();
        frameController.Refresh();

        Debug.Log("프로필 변경 사항이 저장되었습니다.");
    }
}
