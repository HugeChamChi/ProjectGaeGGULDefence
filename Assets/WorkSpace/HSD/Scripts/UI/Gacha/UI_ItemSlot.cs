using UnityEngine;
using UnityEngine.UI;

public class UI_ItemSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image background;

    public void Setup(IItemData itemData)
    {
        if (itemData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        iconImage.sprite = itemData.Icon;
        //frameImage.color = GetFrameColor(itemData.Rarity);
        //background.color = GetBackgroundColor(itemData.Rarity);
    }
}
