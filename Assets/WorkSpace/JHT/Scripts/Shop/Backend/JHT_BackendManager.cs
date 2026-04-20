using BackEnd;
using System;
using UnityEngine;

public class JHT_BackendManager : MonoBehaviour
{
    public LoginSample loginUI;
    public ShopChartManager shopChartManager;
    public ShopScroll shopCanvas;
    public UserProfilePanel userProfilePanel;

    public event Action OnLoadDB;

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
        FindDataManagaer.Instance.LoadAllProfileData();
        FindDataManagaer.Instance.LoadAllItem();

        BackendProfileData.Instance.GetProfileData();

        if (BackendProfileData.userProfileData == null)
        {
            BackendProfileData.Instance.InsertProfileData();
        }

        InitShopData();
        InitProfileData();
    }

    private void InitProfileData()
    {
        userProfilePanel.Init();
    }

    private void InitShopData()
    {
        shopCanvas.Init();
        shopChartManager.InitShopChart();
    }



    #region DB 데이터 세팅

    //private async void InitShopDB()
    //{
    //    try
    //    {
    //        if (shopDBManager == null)
    //            shopDBManager = new ShopDBManager();

    //        // 1. 클라이언트 생성
    //        shopDBClient = new Client("019d4407-a88b-7fc9-88ba-fa7767d88050");

    //        // 2. 초기화 대기
    //        await shopDBClient.Initialize();

    //        Debug.Log("데이터베이스 초기화 완료");

    //        GetShopDB();


    //    }
    //    catch (System.Exception e)
    //    {
    //        Debug.LogError($"DB 초기화 중 오류 발생: {e.Message}");
    //    }
    //}
    //#endregion

    //#region 데이터 가져오기
    //private async void GetShopDB()
    //{
    //    Debug.Log("샵 데이터 조회 시작");
    //    if(shopDBManager.shopDBItem == null)
    //        shopDBManager.shopDBItem = new ShopDBItem();

    //    try
    //    {
    //        var dailyData = await SearchDB(ShopName.Daily);
    //        var weeklyData = await SearchDB(ShopName.Weekly);
    //        var monthlyData = await SearchDB(ShopName.Monthly);

    //        ProcessShopData(dailyData, ShopName.Daily);
    //        ProcessShopData(weeklyData, ShopName.Weekly);
    //        ProcessShopData(monthlyData, ShopName.Monthly);

    //        UpdateDB();
    //    }
    //    catch (System.Exception e)
    //    {
    //        Debug.LogError($"데이터 로드 중 오류 발생: {e.Message}\n{e.StackTrace}"); 
    //    }
    //}

    //private void ProcessShopData(ShopDBItem data, ShopName shopName)
    //{
    //    if (data == null)
    //    {
    //        Debug.Log($"서버에 {shopName} 데이터 없음 - 새로 생성 필요");
    //        return;
    //    }

    //    if (shopDBManager.ListNullCheck(data))
    //    {
    //        // 서버에 아이템 데이터 있음
    //        shopDBManager.ShopDBGetData(data);
    //    }
    //    else
    //    {
    //        // row는 있지만 shopitem이 비어있음 → 랜덤 생성
    //        Debug.Log($"{shopName} 아이템 목록 비어있음 - 랜덤 생성");
    //        shopDBManager.shopDBItem.Shopitem =
    //            FindDataManagaer.Instance.GetRandomItem(shopName, 8);
    //    }
    //}

    //private async Task<ShopDBItem> SearchDB(ShopName _shopName)
    //{
    //    var data = await shopDBClient
    //            .From<ShopDBItem>()
    //            .Where(x => x.ShopName == _shopName.ToString())
    //            .FirstOrDefault();

    //    return data;
    //}

    #endregion

    // Insert 보류

    #region DB 업데이트

    //public async void UpdateDB()
    //{
    //    try
    //    {
    //        Debug.Log("데이터 수정 시작");
    //        var result = await shopDBClient.From<ShopDBItem>().Update(shopDBManager.shopDBItem);
    //        Debug.Log("데이터 수정 완료");
    //    }
    //    catch (System.Exception e)
    //    {
    //        Debug.Log($"업데이트 실패 : {e.Message}");
    //    }
    //}

    //public async void UpdateDBDay(ShopName _shopName)
    //{
    //    shopDBManager.ChangeDBItem(_shopName);

    //    try
    //    {
    //        Debug.Log("데이터 수정 시작");
    //        var result = await shopDBClient.From<ShopDBItem>().Update(shopDBManager.shopDBItem);
    //        Debug.Log("데이터 수정 완료");
    //    }
    //    catch (System.Exception e)
    //    {
    //        Debug.Log($"업데이트 실패 : {e.Message}");
    //    }
    //}

    #endregion
}
