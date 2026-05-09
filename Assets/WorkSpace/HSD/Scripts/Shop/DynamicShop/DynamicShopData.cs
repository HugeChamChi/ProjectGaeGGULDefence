using UnityEngine;

[CreateAssetMenu(fileName = "DynamicShopData", menuName = "Table/DynamicShopData")]
public class DynamicShopData : ScriptableObject
{
    public string ShopID;
    public GameObject ButtonPrefab;
    public GameObject PanelPrefab;
    public int Day;
    public int Hour;
    public float Minute;
    public float Second;
}
