using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BackUserProfilePanel
{
    UserProfilePanel userProfiflePanel;
    public BackUserProfilePanel(UserProfilePanel _panel)
    {
        userProfiflePanel = _panel;
    }

    public void GetData()
    {
        userProfiflePanel.profileModel.curFrameID = BackendProfileData.userProfileData.frameID;
        userProfiflePanel.profileModel.curIconID = BackendProfileData.userProfileData.iconID;

        userProfiflePanel.profileModel.unLockIconList = BackendProfileData.userProfileData.unLockIconHash.ToList();
        userProfiflePanel.profileModel.unLockFrameList = BackendProfileData.userProfileData.unLockFrameHash.ToList();

        userProfiflePanel.profileModel.level = BackendProfileData.userProfileData.level;
        userProfiflePanel.profileModel.nickName = BackendProfileData.userProfileData.nickName;
    }

    public void SaveProfile()
    {
        BackendProfileData.userProfileData.frameID = userProfiflePanel.profileModel.curFrameID;
        BackendProfileData.userProfileData.iconID = userProfiflePanel.profileModel.curIconID;

        BackendProfileData.userProfileData.unLockIconHash = userProfiflePanel.profileModel.unLockIconList.ToHashSet();
        BackendProfileData.userProfileData.unLockFrameHash = userProfiflePanel.profileModel.unLockFrameList.ToHashSet();

        BackendProfileData.Instance.UpdateProfile();
    }
}
