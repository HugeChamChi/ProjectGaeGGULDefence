using UnityEngine;

[CreateAssetMenu(fileName = "DynamicShopTable", menuName = "Shop/DynamicShopTable")]
public class DynamicShopTable : ScriptableObject
{
    public string ShopID;
    public GameObject ButtonPrefab;
    public GameObject PanelPrefab;
    public float Duration;
}
