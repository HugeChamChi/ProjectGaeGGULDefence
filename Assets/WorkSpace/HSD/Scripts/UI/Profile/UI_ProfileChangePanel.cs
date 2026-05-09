using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ProfileItemList : UI_ListBase<IProfileItem, UI_ProfileSlot> { }

public class UI_ProfileChangePanel : UI_ProfilePanelBase
{
    [Header("Components")]
    [SerializeField] UI_PlayerProfileIconSlot profileSlot;
    [SerializeField] ProfileItemList iconList;
    [SerializeField] ProfileItemList frameList;

    [Header("UI")]
    [SerializeField] Button changeButton;
    [SerializeField] Button iconButton;
    [SerializeField] Button frameButton;
    [SerializeField] TextMeshProUGUI unlockDescription;

    [Header("Button Color")]
    [SerializeField] Color selectColor;
    [SerializeField] Color unselectColor;

    private UI_ProfilePresenter _presenter;

    protected override void Awake()
    {
        base.Awake();
        _presenter = new UI_ProfilePresenter(this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        _presenter.Initialize();
        SetupLists();

        iconButton.onClick.AddListener(ChangeIconList);
        frameButton.onClick.AddListener(ChangeFrameList);
        changeButton.onClick.AddListener(_presenter.OnApplyClicked);

        changeButton.interactable = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        iconButton.onClick.RemoveListener(ChangeIconList);
        frameButton.onClick.RemoveListener(ChangeFrameList);
        changeButton.onClick.RemoveListener(_presenter.OnApplyClicked);
    }

    private void SetupLists()
    {
        iconList.Setup(Table.Profile.GetItemByType(ProfileItemType.Icon));
        foreach (var slot in iconList.GetActiveSlots())
            slot.SetCallback(_presenter.OnItemSelected);

        frameList.Setup(Table.Profile.GetItemByType(ProfileItemType.Frame));
        foreach (var slot in frameList.GetActiveSlots())
            slot.SetCallback(_presenter.OnItemSelected);

        ChangeIconList();
    }

    public void UpdatePreview(IProfileItem icon, IProfileItem frame)
    {
        profileSlot.Setup(icon);
        profileSlot.Setup(frame);
    }

    public void UpdateApplyButtonState(bool isInteractable)
    {
        changeButton.interactable = isInteractable;
    }

    public void RefreshList()
    {
        iconList.RefreshAll();
        frameList.RefreshAll();
    }

    private void ChangeIconList()
    {
        iconButton.image.color = selectColor;
        frameButton.image.color = unselectColor;

        iconList.SetActive(true);
        frameList.SetActive(false);
    }

    private void ChangeFrameList()
    {
        iconButton.image.color = unselectColor;
        frameButton.image.color = selectColor;

        iconList.SetActive(false);
        frameList.SetActive(true);
    }
}
