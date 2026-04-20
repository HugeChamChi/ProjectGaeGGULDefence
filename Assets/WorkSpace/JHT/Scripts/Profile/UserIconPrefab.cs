using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UserIconPrefab : UserPrfilePrefab
{
    [SerializeField] private Button userIconButton;
    [SerializeField] private GameObject blockImg;
    [SerializeField] private Image userIconImg;

    UserProfilePanel userProfilePanel;

    private void Awake()
    {
        userIconImg = GetComponentInChildren<Image>();
    }

    private void OnDestroy()
    {
        UnSubscribe();
    }

    private void UnSubscribe()
    {
        OnOpen -= OpenIcon;
        userIconButton.onClick.RemoveAllListeners();
    }

    public void Init(UserProfilePanel _userProfilePanel, bool _isOpen)
    {
        UnSubscribe();

        userProfilePanel = _userProfilePanel;
        OnOpen += OpenIcon;

        IsOpen = _isOpen;
        userIconImg.sprite = dataSO.dataSprite;

        userIconButton.onClick.AddListener(ClickIcon);
    }

    protected override void ClickIcon()
    {
        if (isOpen == false)
            return;

        userProfilePanel.OnChangeIcon?.Invoke(dataSO.dataSprite, dataSO.ID);
    }

    private void OpenIcon(bool value)
    {
        blockImg.SetActive(!value);
    }
}

