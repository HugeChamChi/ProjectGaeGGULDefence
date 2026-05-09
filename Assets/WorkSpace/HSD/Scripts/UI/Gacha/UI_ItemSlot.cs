using UnityEngine;
using UnityEngine.UI;

public class UI_ItemSlot : UI_SlotBase<IItemData>
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image background;

    protected override void OnBind()
    {
        if (_data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        if (iconImage != null) iconImage.sprite = _data.Icon;
        // frameImage.color = GetFrameColor(_data.Rarity);
        // background.color = GetBackgroundColor(_data.Rarity);
    }
}
