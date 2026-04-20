using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ProfileChooseIconPanel : MonoBehaviour
{
    List<UserIconPrefab> iconList;
    [SerializeField] private Transform iconParent;

    private UserProfilePanel userProfilePanel;

    public void Init(UserProfilePanel _userProfilePanel)
    {
        userProfilePanel = _userProfilePanel;
        ListInit();
    }

    private void ListInit()
    {
        iconList = new List<UserIconPrefab>();
        for (int i = 0; i < iconParent.childCount; i++)
        {
            iconList.Add(iconParent.GetChild(i).GetComponent<UserIconPrefab>());
        }
    }

    public void SetIconPrefab()
    {
        for (int i = 0; i < iconParent.childCount; i++)
        {
            var prefab = iconParent.GetChild(i).GetComponent<UserIconPrefab>();
            
            if (userProfilePanel.profileModel.unLockFrameList.Contains(prefab.dataSO.ID))
            {
                prefab.Init(userProfilePanel, true);
            }
            else
                prefab.Init(userProfilePanel, false);
        }
    }

    public void OpenIconPrefab(int _id)
    {
        var data = iconList.Where(x => x.dataSO.ID == _id).FirstOrDefault();

        if (userProfilePanel.profileModel.unLockFrameList.Contains(data.dataSO.ID))
        {
            data.Init(userProfilePanel, true);
        }
    }
}
