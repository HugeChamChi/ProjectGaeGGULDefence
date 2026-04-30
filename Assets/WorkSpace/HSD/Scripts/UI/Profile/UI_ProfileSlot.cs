using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ProfileSlot : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] Image frameImage;
    [SerializeField] Image equipeedIcon;
    [SerializeField] Image lockIcon;
    [SerializeField] Image lockImage;
    [SerializeField] TextMeshProUGUI nameText;

    [SerializeField] Button selectButton;

    private IProfileItem data;

    public void SetData(IProfileItem data, Action<IProfileItem> onClick = null)
    {
        this.data = data;

        if(data.Type == ProfileItemType.Icon)
        {
            iconImage.sprite = data.Sprite;
        }
        else if(data.Type == ProfileItemType.Frame)
        {
            frameImage.sprite = data.Sprite;
        }

        Refresh();

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onClick?.Invoke(data));
    }

    public void Refresh()
    {
        if (data == null) return;

        nameText.text = data.Name;

        lockImage.gameObject.SetActive(!data.IsUnlocked);
        lockIcon.gameObject.SetActive(!data.IsUnlocked);

        bool isEquipped = false;
        if (data.Type == ProfileItemType.Icon)
            isEquipped = data.Id == User.Profile.CurrentIconData?.Id;
        else
            isEquipped = data.Id == User.Profile.CurrentFrameData?.Id;

        equipeedIcon.gameObject.SetActive(isEquipped);
    }
}