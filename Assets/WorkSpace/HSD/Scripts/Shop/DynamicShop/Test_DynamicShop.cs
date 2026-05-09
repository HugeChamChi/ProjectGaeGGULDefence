using UnityEngine;
using Cysharp.Threading.Tasks;

public class Test_DynamicShop : MonoBehaviour
{
    [Header("Testing Configuration")]
    [SerializeField] private string _testShopID = "TestShop_01";

    [ContextMenu("Trigger Shop")]
    [Button]
    public void TriggerShop()
    {
        TriggerShopAsync().Forget();
    }

    private async UniTaskVoid TriggerShopAsync()
    {
        Debug.Log($"<color=cyan>[Test_DynamicShop]</color> Triggering Shop: {_testShopID}");
        await Player.Shop.Dynamic.TriggerShopAsync(_testShopID);
    }

    [ContextMenu("Complete Shop")]
    [Button]
    public void CompleteShop()
    {
        CompleteShopAsync().Forget();
    }

    private async UniTaskVoid CompleteShopAsync()
    {
        Debug.Log($"<color=orange>[Test_DynamicShop]</color> Completing Shop: {_testShopID}");
        await Player.Shop.Dynamic.CompleteShopAsync(_testShopID);
    }

    [ContextMenu("Check Active Shops")]
    [Button]
    public void CheckActiveShops()
    {
        Debug.Log("<color=yellow>[Test_DynamicShop]</color> Checking Active Shops (Manual Refresh)");
        Player.Shop.Dynamic.CheckActiveShops();
    }
}
