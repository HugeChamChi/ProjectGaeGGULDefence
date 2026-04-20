using UnityEngine;
using UnityEngine.UI;

public class UserFramePrefab : UserPrfilePrefab
{
    [SerializeField] private Button userFrameButton;
    [SerializeField] private GameObject blockImg;
    [SerializeField] private Image userFrameImg;
    public Color frameColor { get; private set; }

    UserProfilePanel userProfilePanel;

    private void OnEnable()
    {
        if (userFrameImg == null)
            userFrameImg = GetComponentInChildren<Image>();
    }

    private void OnDestroy()
    {
        UnSubscribe();
    }

    public void Init(UserProfilePanel _userProfilePanel,bool _isOpen)
    {
        UnSubscribe();

        userProfilePanel = _userProfilePanel;
        OnOpen += OpenFrame;

        IsOpen = _isOpen;
        userFrameImg.sprite = dataSO.dataSprite;

        userFrameButton.onClick.AddListener(ClickIcon);
        frameColor = userFrameImg.color;
    }

    protected override void ClickIcon()
    {
        if (isOpen == false)
            return;

        userProfilePanel.OnChangeFrame?.Invoke(dataSO.dataSprite, dataSO.ID, frameColor);
    }

    private void OpenFrame(bool value)
    {
        blockImg.SetActive(!value);
    }

    private void UnSubscribe()
    {
        OnOpen -= OpenFrame;
        userFrameButton.onClick.RemoveAllListeners();
    }
}
