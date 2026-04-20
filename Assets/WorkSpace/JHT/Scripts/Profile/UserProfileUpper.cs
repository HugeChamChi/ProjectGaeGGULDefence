using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserProfileUpper : MonoBehaviour
{
    [Header("Basic Profile")]
    [SerializeField] private TextMeshProUGUI nickName_Text;
    [SerializeField] private TextMeshProUGUI level_Text;

    [Header("Profile Image Change")]
    [SerializeField] private Image userProfileIcon;
    [SerializeField] private Image userProfileFrame;

    UserProfilePanel userProfilePanel;
    public void Init(UserProfilePanel _userProfilePanel)
    {
        userProfilePanel = _userProfilePanel;

        userProfileIcon.sprite =
                FindDataManagaer.Instance.IntToData(_userProfilePanel.profileModel.curIconID,ProfileType.Icon).dataSprite;
            
        userProfileFrame.sprite =
                FindDataManagaer.Instance.IntToData(_userProfilePanel.profileModel.curFrameID, ProfileType.Frame).dataSprite;

        nickName_Text.text = userProfilePanel.profileModel.nickName;
        level_Text.text = userProfilePanel.profileModel.level.ToString();

        Subscribe();
    }

    private void OnDisable()
    {
        UnSubscribe();
    }

    /// <summary>
    /// 프로필 아이톤, 프레임 변경
    /// </summary>
    /// <param name="_sprite"></param>
    /// <param name="_ID"></param>
    private void ChangeIcon(Sprite _sprite, int _ID)
    {
        userProfileIcon.sprite = _sprite;
        userProfilePanel.profileModel.curIconID = _ID;
    }

    private void ChangeFrame(Sprite _sprite, int _ID, Color _color)
    {
        userProfileFrame.sprite = _sprite;
        userProfileFrame.color = _color;
        userProfilePanel.profileModel.curFrameID = _ID;
    }



    #region Event Subscribe
    private void Subscribe()
    {
        userProfilePanel.OnChangeIcon += ChangeIcon;
        userProfilePanel.OnChangeFrame += ChangeFrame;
    }

    private void UnSubscribe()
    {
        userProfilePanel.OnChangeIcon -= ChangeIcon;
        userProfilePanel.OnChangeFrame -= ChangeFrame;
    }
    #endregion
}
