using BackEnd;
using UnityEngine;

internal class BackendShopItemData
{
    private static BackendShopItemData instance = null;
    public static BackendShopItemData Instance
    {
        get
        {
            if (instance == null)
                instance = new BackendShopItemData();

            return instance;
        }
    }

    public static ShopItemData shopItemData;
    private string gameDataRowInDate = string.Empty;

    public void ShopItemDataInsert_All()
    {
        if (shopItemData == null)
        {
            Debug.Log("샵 아이템 데이터 정보 없음 새로생성");
            shopItemData = new ShopItemData();
        }

        shopItemData.daily_Item =
            FindDataManagaer.Instance.GetRandItem(FindDataManagaer.Instance.FindItemList(ShopType.Daily), 8);

        shopItemData.weekly_Item =
            FindDataManagaer.Instance.GetRandItem(FindDataManagaer.Instance.FindItemList(ShopType.Weekly), 8);

        shopItemData.monthly_Item =
            FindDataManagaer.Instance.GetRandItem(FindDataManagaer.Instance.FindItemList(ShopType.Monthly), 8);


        Param param = new Param();
        param.Add("daily_Item", shopItemData.daily_Item);
        param.Add("weekly_Item", shopItemData.weekly_Item);
        param.Add("monthly_Item", shopItemData.monthly_Item);

        BackendReturnObject bro = Backend.GameData.Insert("SHOP_ITEM_DATA", param);

        if (bro.IsSuccess())
        {
            Debug.Log("게임 정보 데이터 삽입 성공 : " + bro);

            gameDataRowInDate = bro.GetInDate();
        }
        else
        {
            Debug.LogError("게임 정보 데이터 삽입 실패 : " + bro);
        }
    }


    public void ShopDataGet()
    {
        var bro = Backend.GameData.GetMyData("SHOP_ITEM_DATA", new Where());

        if (bro.IsSuccess())
        {
            LitJson.JsonData gameJsonData = bro.FlattenRows();

            if (gameJsonData.Count <= 0)
            {
                Debug.LogWarning("데이터가 존재하지 않습니다");
            }
            else
            {
                //불러온 게임 정보의 고유값
                gameDataRowInDate = gameJsonData[0]["inDate"].ToString();

                shopItemData = new ShopItemData();

                foreach (string itemKey in gameJsonData[0]["daily_Item"])
                {
                    shopItemData.daily_Item.Add(int.Parse(itemKey));
                }
                foreach (string itemKey in gameJsonData[0]["weekly_Item"])
                {
                    shopItemData.weekly_Item.Add(int.Parse(itemKey));
                }
                foreach (string itemKey in gameJsonData[0]["monthly_Item"])
                {
                    shopItemData.monthly_Item.Add(int.Parse(itemKey));
                }

                Debug.Log(shopItemData.ToString());
            }

        }
        else
        {
            Debug.LogError("게임 정보 조회 실패 : " + bro);
        }
    }



    public void ShopItemUpdate_Daily()
    {
        if (shopItemData == null)
        {
            Debug.LogError("BackendShopItemData : shopItemData가 없음 Daily 진행중.");
            return;
        }

        shopItemData.daily_Item = 
            FindDataManagaer.Instance.GetRandItem(FindDataManagaer.Instance.FindItemList(ShopType.Daily), 8);

        Param param = new Param();
        param.Add("daily_Item", shopItemData.daily_Item);

        BackendReturnObject bro = null;

        if (string.IsNullOrEmpty(gameDataRowInDate))
        {
            Debug.Log("내 제일 최신 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.Update("SHOP_ITEM_DATA", new Where(), param);
        }
        else
        {
            Debug.Log($"{gameDataRowInDate}의 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.UpdateV2("SHOP_ITEM_DATA", gameDataRowInDate, Backend.UserInDate, param);
        }

        if (bro.IsSuccess())
        {
            Debug.Log("게임 정보 데이터 수정에 성공했습니다. : " + bro);
        }
        else
        {
            Debug.LogError("게임 정보 데이터 수정에 실패했습니다. : " + bro);
        }
    }

    public void ShopItemUpdate_Weekly()
    {
        if (shopItemData == null)
        {
            Debug.LogError("BackendShopItemData : shopItemData가 없음 Weekly 진행중.");
            return;
        }

        shopItemData.weekly_Item =
            FindDataManagaer.Instance.GetRandItem(FindDataManagaer.Instance.FindItemList(ShopType.Weekly), 8);

        Param param = new Param();
        param.Add("weekly_Item", shopItemData.weekly_Item);

        BackendReturnObject bro = null;

        if (string.IsNullOrEmpty(gameDataRowInDate))
        {
            Debug.Log("내 제일 최신 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.Update("SHOP_ITEM_DATA", new Where(), param);
        }
        else
        {
            Debug.Log($"{gameDataRowInDate}의 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.UpdateV2("SHOP_ITEM_DATA", gameDataRowInDate, Backend.UserInDate, param);
        }

        if (bro.IsSuccess())
        {
            Debug.Log("게임 정보 데이터 수정에 성공했습니다. : " + bro);
        }
        else
        {
            Debug.LogError("게임 정보 데이터 수정에 실패했습니다. : " + bro);
        }
    }

    public void ShopItemUpdate_Monthly()
    {
        if (shopItemData == null)
        {
            Debug.LogError("BackendShopItemData : shopItemData가 없음 Monthly 진행중.");
            return;
        }

        shopItemData.monthly_Item =
            FindDataManagaer.Instance.GetRandItem(FindDataManagaer.Instance.FindItemList(ShopType.Monthly), 8);

        Param param = new Param();
        param.Add("monthly_Item", shopItemData.monthly_Item);

        BackendReturnObject bro = null;

        if (string.IsNullOrEmpty(gameDataRowInDate))
        {
            Debug.Log("내 제일 최신 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.Update("SHOP_ITEM_DATA", new Where(), param);
        }
        else
        {
            Debug.Log($"{gameDataRowInDate}의 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.UpdateV2("SHOP_ITEM_DATA", gameDataRowInDate, Backend.UserInDate, param);
        }

        if (bro.IsSuccess())
        {
            Debug.Log("게임 정보 데이터 수정에 성공했습니다. : " + bro);
        }
        else
        {
            Debug.LogError("게임 정보 데이터 수정에 실패했습니다. : " + bro);
        }
    }
}
