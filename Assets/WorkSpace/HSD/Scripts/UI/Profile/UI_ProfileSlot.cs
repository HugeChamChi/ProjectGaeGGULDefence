using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ProfileSlot : UI_SlotBase<IProfileItem>
{
    [SerializeField] Image iconImage;
    [SerializeField] Image frameImage;
    [SerializeField] Image equipeedIcon;
    [SerializeField] Image lockIcon;
    [SerializeField] Image lockImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Button selectButton;

    private Action<IProfileItem> _onSelect;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(() => _onSelect?.Invoke(_data));
    }

    public void SetCallback(Action<IProfileItem> onSelect) => _onSelect = onSelect;

    protected override void OnBind()
    {
        if (_data == null) return;

        if (_data.Type == ProfileItemType.Icon)
        {
            if (iconImage != null) iconImage.sprite = _data.Sprite;
        }
        else if (_data.Type == ProfileItemType.Frame)
        {
            if (frameImage != null) frameImage.sprite = _data.Sprite;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_data == null) return;

        if (nameText != null) nameText.text = _data.Name;
        if (lockImage != null) lockImage.gameObject.SetActive(!_data.IsUnlocked);
        if (lockIcon != null) lockIcon.gameObject.SetActive(!_data.IsUnlocked);

        bool isEquipped = false;
        if (_data.Type == ProfileItemType.Icon)
            isEquipped = _data.Id == Player.Profile.CurrentIconData?.Id;
        else
            isEquipped = _data.Id == Player.Profile.CurrentFrameData?.Id;

        if (equipeedIcon != null) equipeedIcon.gameObject.SetActive(isEquipped);
    }
}