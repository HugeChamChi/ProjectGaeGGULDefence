using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ProfileChooseFramePanel : MonoBehaviour
{
    [SerializeField] private Transform frameParent;
    private List<UserFramePrefab> frameList;
    UserProfilePanel userProfilePanel;

    public void Init(UserProfilePanel _userProfilePanel)
    {
        userProfilePanel = _userProfilePanel;
        ListInit();
    }

    private void ListInit()
    {
        frameList = new List<UserFramePrefab>();
        for (int i = 0; i < frameParent.childCount; i++)
        {
            frameList.Add(frameParent.GetChild(i).GetComponent<UserFramePrefab>());
        }
    }

    public void SetFramePrefab()
    {
        for (int i = 0; i < frameParent.childCount; i++)
        {
            var prefab = frameParent.GetChild(i).GetComponent<UserFramePrefab>();

            if (userProfilePanel.profileModel.unLockFrameList.Contains(prefab.dataSO.ID))
                prefab.Init(userProfilePanel, true);
            else
                prefab.Init(userProfilePanel, false);
        }
    }

    public void OpenFramePrefab(int _id)
    {
        var data = frameList.Where(x => x.dataSO.ID == _id).FirstOrDefault();

        if (userProfilePanel.profileModel.unLockFrameList.Contains(data.dataSO.ID))
        {
            data.Init(userProfilePanel, true);
        }
    }
}
