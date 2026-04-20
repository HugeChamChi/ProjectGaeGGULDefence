using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopSO")]
public class ShopDataSO : ScriptableObject
{
    public int shopID;
    public string shopType;
    public string shopInfo;

    public ShopItemPrefab[] shopItemPrefab;
}
