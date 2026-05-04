using System.Collections.Generic;
using UnityEngine;

public class DynamicShopPopupManager : MonoBehaviour
{
    [SerializeField] private Transform _uiRoot; // 패널이 생성될 Canvas 또는 UI 루트
    private Dictionary<string, RectTransform> _panelDict = new();

    private void Start()
    {
        // 상점 활성화 이벤트 구독
        Player.Shop.Dynamic.OnShopActivated += OnShopActivated;
    }

    private void OnDestroy()
    {
        if (Player.Shop.Dynamic != null)
            Player.Shop.Dynamic.OnShopActivated -= OnShopActivated;
    }

    private void OnShopActivated(string shopID, bool isFirstTime)
    {
        // 처음 활성화되는 경우(이벤트 발생 직후)에만 자동으로 패널을 띄움
        if (isFirstTime)
        {
            OpenPanel(shopID);
        }
    }

    public void OpenPanel(string shopID)
    {
        var data = Table.Shop.Dynamic.Get(shopID);

        if (_panelDict.TryGetValue(shopID, out var panel))
        {
            panel.gameObject.SetActive(true);
            return;
        }

        if (data != null && data.PanelPrefab != null)
        {
            // 지정된 _uiRoot 하위에 패널 생성
            GameObject panelObj = RM.Instantiate(data.PanelPrefab, _uiRoot);

            // RectTransform을 초기화하여 부모에 꽉 차도록 설정
            if (panelObj.TryGetComponent<RectTransform>(out var rect))
            {
                SetFull(rect);
                _panelDict.TryAdd(shopID, rect);
            }
        }
    }

    private void SetFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
