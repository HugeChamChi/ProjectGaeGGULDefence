using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserProfilePanel : MonoBehaviour
{
    #region UI Data
    [Header("Panel Active Button")]
    [SerializeField] private Button userIconButton;
    [SerializeField] private Button userFrameButton;
    [SerializeField] private Button closeButton;

    [Header("Button Color")]
    [SerializeField] private Image userIconButtonImage;
    [SerializeField] private Image userFrameButtonImage;

    [Header("Button color")]
    [SerializeField] private Color chooseColor;
    [SerializeField] private Color unChooseColor;



    [Header("Active Panel")]
    public ProfileChooseIconPanel userIconPanel;
    public ProfileChooseFramePanel userFramePanel;
    public GameObject profilePanel;

    #endregion

    public UserProfileUpper profileUpper;
    public ProfileData profileModel;
    public BackUserProfilePanel backUserProfile;

    public Action<Sprite,int> OnChangeIcon;
    public Action<Sprite, int, Color> OnChangeFrame;
    public Action<int> OnOpenIconID;
    public Action<int> OnOpenFrameID;

    /// <summary>
    /// Demo에서 Invoke, 여기서 이벤트 등록
    /// </summary>
    public Action<int> OnUnLockIcon;
    public Action<int> OnUnLockFrame;

    public event Action<int,int> OnProfileInit;

    private void Awake()
    {
        ListInit();

        userIconPanel = GetComponentInChildren<ProfileChooseIconPanel>(true);
        userFramePanel = GetComponentInChildren<ProfileChooseFramePanel>(true);
    }


    private void OnApplicationQuit()
    {
        UnSubscribe();
        backUserProfile.SaveProfile();
    }

    private void ListInit()
    {
        if (profileModel.unLockIconList == null)
            profileModel.unLockIconList = new List<int>();

        if (profileModel.unLockFrameList == null)
            profileModel.unLockFrameList = new List<int>();

    }

    public void Init()
    {
        Subscribe();

        profileModel = new ProfileData();
        backUserProfile = new BackUserProfilePanel(this);

        // 서버에서 데이터 가져오기
        backUserProfile.GetData();
        ProfileChooseInit();
    }

    private void ProfileChooseInit()
    {
        userIconPanel.Init(this);
        userFramePanel.Init(this);
    }

    public void OpenProfile()
    {
        profilePanel.SetActive(true);
        profileUpper.Init(this);

        OpenIconPanel();
        userIconPanel.SetIconPrefab();

        OpenFramePanel();
        userFramePanel.SetFramePrefab();
    }

    /// <summary>
    /// 아이콘, 프레임 패널 열기 버튼
    /// </summary>
    private void OpenIconPanel()
    {
        CloseFramePanel();
        userIconPanel.gameObject.SetActive(true);
        userIconButtonImage.color = chooseColor;
    }

    private void OpenFramePanel()
    {
        CloseIconPanel();
        userFramePanel.gameObject.SetActive(true);
        userFrameButtonImage.color = chooseColor;
    }

    private void CloseIconPanel()
    {
        userIconPanel.gameObject.SetActive(false);
        userIconButtonImage.color = unChooseColor;
    }

    private void CloseFramePanel()
    {
        userFramePanel.gameObject.SetActive(false);
        userFrameButtonImage.color = unChooseColor;
    }


    /// <summary>
    /// 해금
    /// </summary>
    /// <param name="_id"></param>
    private void UnlockIcon(int _id)
    {
        if (!profileModel.unLockIconList.Contains(_id))
        {
            profileModel.unLockIconList.Add(_id);
            userIconPanel.OpenIconPrefab(_id);
        }
    }

    private void UnlockFrame(int _id)
    {
        if (!profileModel.unLockFrameList.Contains(_id))
        {
            profileModel.unLockFrameList.Add(_id);
            userFramePanel.OpenFramePrefab(_id);
        }
    }

    private void CloseProfile()
    {
        profilePanel.SetActive(false);
    }

    #region Event Subscribe
    private void Subscribe()
    {
        OnUnLockIcon += UnlockIcon;
        OnUnLockFrame += UnlockFrame;

        userIconButton.onClick.AddListener(OpenIconPanel);
        userFrameButton.onClick.AddListener(OpenFramePanel);

        closeButton.onClick.AddListener(CloseProfile);
    }

    private void UnSubscribe()
    {
        OnUnLockIcon -= UnlockIcon;
        OnUnLockFrame -= UnlockFrame;


        userIconButton.onClick.RemoveAllListeners();
        userFrameButton.onClick.RemoveAllListeners();
        closeButton.onClick.RemoveAllListeners();
    }
    #endregion
}
