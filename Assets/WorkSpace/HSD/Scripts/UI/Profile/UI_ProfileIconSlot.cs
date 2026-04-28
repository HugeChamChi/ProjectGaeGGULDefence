using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UI_ProfileIconSlot : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Button button;

    public void Setup(Sprite iconSprite, UnityAction buttonEvent)
    {
        if(button == null)
        {
            button.interactable = false;
            icon.sprite = iconSprite;
            return;
        }

        button.onClick.RemoveAllListeners();

        icon.sprite = iconSprite;
        button.onClick.AddListener(buttonEvent);
    }
}
