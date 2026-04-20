using BackEnd;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackendFunction
{
    
    public void SendFunc()
    {
        ShopItemData data = BackendShopItemData.shopItemData;

        // JSON 변환
        string json = JsonUtility.ToJson(data);

        // Param 생성
        Param param = new Param();
        param.Add("shopData", json);

        // 호출
        Backend.BFunc.InvokeFunction("CreateShop", param, callback =>
        {
            if (callback.IsSuccess())
            {
                Debug.Log("성공");

                for (int i = 0; i < BackendShopItemData.shopItemData.daily_Item.Count; i++)
                {
                    Debug.Log($"item id : {BackendShopItemData.shopItemData.daily_Item[i]}");
                }
            }
        });
    }
}
