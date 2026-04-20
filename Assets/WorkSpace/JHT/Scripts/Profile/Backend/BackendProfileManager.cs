using BackEnd;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackendProfileManager : MonoBehaviour
{
    public LoginProfile loginUI;
    public UserProfilePanel userProfilePanel;
    //public ChartManager chartManager;
    //public ShopChartManager shopChartManager;
    public ShopScroll shopCanvas;

    private void OnEnable()
    {
        loginUI.OnLoginFinish += InsertData;
    }

    private void OnDisable()
    {
        loginUI.OnLoginFinish -= InsertData;
    }

    private void Start()
    {
        BackendSet();
    }

    private void BackendSet()
    {
        var bro = Backend.Initialize();

        if (bro.IsSuccess())
        {
            loginUI.Init();
        }
        else
        {
            Debug.LogError("초기화 실패 : " + bro);
            loginUI.BtnActive(true);
        }
    }

    private void InsertData()
    {
        BackendProfileData.Instance.GetProfileData();

        if (BackendProfileData.userProfileData == null)
        {
            BackendProfileData.Instance.InsertProfileData();
        }

        //userProfilePanel.GetData();
    }

}
