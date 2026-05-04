using System.Collections.Generic;
using UnityEngine;

public class DynamicShopSpawner : MonoBehaviour
{
    [SerializeField] private Transform _parent; // 버튼이 생성될 위치
    [SerializeField] private DynamicShopPopupManager _popupManager; // 필드 주입을 위한 참조

    private Dictionary<string, GameObject> _spawnedButtons = new Dictionary<string, GameObject>();

    private void Start()
    {
        // 이벤트 구독
        Player.Shop.Dynamic.OnShopActivated += OnShopActivated;
        Player.Shop.Dynamic.OnShopCompleted += OnShopCompleted;
        
        // 이미 활성화된 상점들 스폰
        Player.Shop.Dynamic.CheckActiveShops();
    }

    private void OnDestroy()
    {
        if (Player.Shop.Dynamic != null)
        {
            Player.Shop.Dynamic.OnShopActivated -= OnShopActivated;
            Player.Shop.Dynamic.OnShopCompleted -= OnShopCompleted;
        }
    }

    private void OnShopActivated(string shopID, bool isFirstTime)
    {
        // 이미 생성된 버튼이 있다면 무시
        if (_spawnedButtons.ContainsKey(shopID)) return;

        var shopData = Table.Shop.Dynamic.Get(shopID);
        if (shopData == null) return;

        if (Player.Shop.Dynamic.IsCompleted(shopID)) return;

        // 버튼 생성
        var buttonObj = RM.Instantiate(shopData.ButtonPrefab, _parent, false);
        if (buttonObj.TryGetComponent<UI_DynamicShopButton>(out var btn))
        {
            // PopupManager를 버튼에 주입
            btn.Initialize(shopData, _popupManager);
        }

        _spawnedButtons[shopID] = buttonObj;
    }

    private void OnShopCompleted(string shopID)
    {
        if (_spawnedButtons.TryGetValue(shopID, out var buttonObj))
        {
            // 버튼 제거
            RM.Destroy(buttonObj);
            _spawnedButtons.Remove(shopID);
        }
    }
}
