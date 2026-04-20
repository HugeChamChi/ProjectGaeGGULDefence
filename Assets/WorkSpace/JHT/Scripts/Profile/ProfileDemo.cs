using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProfileDemo : MonoBehaviour
{
    public Button frameOpenButton;
    public Button iconOpenButton;

    public UserProfilePanel userProfilePanel;
    private void OnEnable()
    {
        if (userProfilePanel == null)
            userProfilePanel = FindObjectOfType<UserProfilePanel>();

        frameOpenButton.onClick.AddListener(FrameOpen);
        iconOpenButton.onClick.AddListener(IconOpen);
    }

    private void OnDisable()
    {
        frameOpenButton.onClick.RemoveAllListeners();
        iconOpenButton.onClick.RemoveAllListeners();
    }

    private void FrameOpen()
    {
        userProfilePanel.OnUnLockFrame?.Invoke(Random.Range(0, FindDataManagaer.Instance.frameList.Count));
    }

    private void IconOpen()
    {
        userProfilePanel.OnUnLockIcon?.Invoke(Random.Range(0,FindDataManagaer.Instance.iconList.Count));
    }

    private void ProfileOpen()
    {
        userProfilePanel.OpenProfile();
        //userProfilePanel.ProfileInit();
    }
}
