using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ShopPanel : MonoBehaviour
{
    public ShopType shopType;

    protected void Start() => GetData();

    protected abstract void GetData();
    public abstract void Init(ShopChartManager _shopChartManager);
}
