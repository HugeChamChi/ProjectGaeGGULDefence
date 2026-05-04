using System.Collections.Generic;
using UnityEngine;

public class DynamicShopSpawner : MonoBehaviour
{
    [SerializeField] private List<DynamicShopTable> _shopList;
    [SerializeField] private Transform _parent;

    private Dictionary<string, DynamicShopTable> _shopTableDict = new Dictionary<string, DynamicShopTable>();

    private void Awake()
    {
        foreach (var shop in _shopList)
        {
            _shopTableDict[shop.ShopID] = shop;
        }
    }

    private void Start()
    {
        // 이벤트 구독
        Player.Shop.Dynamic.OnShopActivated += OnShopActivated;
        
        // 이미 활성화된 상점들 스폰
        Player.Shop.Dynamic.CheckActiveShops();
    }

    private void OnDestroy()
    {
        if (Player.Shop.Dynamic != null)
            Player.Shop.Dynamic.OnShopActivated -= OnShopActivated;
    }

    private void OnShopActivated(string shopID, bool isFirstTime)
    {
        if (!_shopTableDict.TryGetValue(shopID, out var shopTable)) return;

        // 이미 완료된 경우 무시 (DataManager에서 걸러지지만 이중 확인)
        if (Player.Shop.Dynamic.IsCompleted(shopID)) return;

        // 버튼 생성
        var buttonObj = RM.Instantiate(shopTable.ButtonPrefab, _parent, false);
        if (buttonObj.TryGetComponent<UI_DynamicShopButton>(out var btn))
        {
            btn.Initialize(shopTable);
        }

        // 처음 이벤트로 인한 활성화라면 패널도 즉시 띄움
        if (isFirstTime)
        {
            RM.Instantiate(shopTable.PanelPrefab);
        }
    }
}
