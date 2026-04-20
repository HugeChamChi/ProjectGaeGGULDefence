using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopScroll : MonoBehaviour
{
    public ScrollRect scrollRect;
    public RectTransform content;

    public bool isLoading;

    public float scrollBound;

    public List<GameObject> shopList;
    private int nextOpenIndex;


    public void Init()
    {
        InitList();
        scrollRect.onValueChanged.AddListener(OnScroll);

        BackendShopData.Instance.OnShopDataLoaded += LoadItems;
        BackendShopData.Instance.OnShopDataLoaded += CallbackInit;


        foreach (var data in shopList)
            data.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (BackendShopData.Instance != null)
        {
            BackendShopData.Instance.OnShopDataLoaded -= LoadItems;
            BackendShopData.Instance.OnShopDataLoaded -= CallbackInit;
        }
    }

    private void InitList()
    {
        shopList = new List<GameObject>();

        nextOpenIndex = 0;

        for (int i = 0; i < content.childCount; i++)
        {
            shopList.Add(content.GetChild(i).gameObject);
        }
    }
    private void CallbackInit(ShopChartManager _shopChartManager)
    {
        foreach (var shopObj in shopList)
        {
            ShopPanel panel = shopObj.GetComponent<ShopPanel>();
            if (panel == null)
            {
                Debug.LogError("ShopPanel ¾øÀ½: " + shopObj.name);
                continue;
            }
            else
            {
                panel.Init(_shopChartManager);
            }
        }
    }

    private void OnScroll(Vector2 position)
    {
        if (isLoading) 
            return;

        if (position.y <= scrollBound)
        {
            isLoading = true;
            LoadItems(null);
        }
    }

    private void LoadItems(ShopChartManager _shopChartManager)
    {
        for (int i = 0; i < 2; i++)
        {
            if (nextOpenIndex <= shopList.Count - 1)
            {
                shopList[nextOpenIndex].gameObject.SetActive(true);
                nextOpenIndex++;
            }
        }

        isLoading = false;
    }

}
