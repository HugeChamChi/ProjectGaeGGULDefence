using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UI_PlayerProfileIconSlot : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Image frame;
    [SerializeField] Button button;

    private void OnEnable()
    {
        SetDefault();
    }

    public void SetDefault()
    {
        icon.sprite = User.Profile.CurrentIconSprite;
        frame.sprite = User.Profile.CurrentFrameSprite;
    }

    public void Setup(IProfileItem item)
    {
        if (item == null) return;

        if (item.Type == ProfileItemType.Icon)
        {
            icon.sprite = item.Sprite;
        }
        else if (item.Type == ProfileItemType.Frame)
        {
            frame.sprite = item.Sprite;
        }
    }

    public void SetButtonEvent(UnityAction buttonEvent = null)
    {
        if (button == null)
        {
            return;
        }

        if (buttonEvent == null)
        {
            button.interactable = false;
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(buttonEvent);
    }
}
