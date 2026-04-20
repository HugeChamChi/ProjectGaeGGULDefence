using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShopItemModel
{
    public ShopData shopData;
    public Item itemSO;

    private int purchaseCount;
    public int PurchaseCount { get { return purchaseCount; } set { purchaseCount = value; OnChangePurchase?.Invoke(purchaseCount); } }
    public event Action<int> OnChangePurchase;
}
