using System.Collections.Generic;
using UnityEngine;

public class UI_ShopListController : MonoBehaviour
{
    [SerializeField] private UI_ShopItemSlot slotPrefab;
    [SerializeField] private Transform slotContainer;

    private List<UI_ShopItemSlot> _slots = new List<UI_ShopItemSlot>();

    public void Setup(List<ShopItemData> items)
    {
        // 기존 슬롯 제거 또는 재사용
        foreach (var slot in _slots)
        {
            slot.gameObject.SetActive(false);
        }

        for (int i = 0; i < items.Count; i++)
        {
            UI_ShopItemSlot slot;
            if (i < _slots.Count)
            {
                slot = _slots[i];
                slot.gameObject.SetActive(true);
            }
            else
            {
                slot = RM.Instantiate(slotPrefab, slotContainer, true);
                _slots.Add(slot);
            }

            slot.Init(items[i]);
        }
    }

    public void Refresh()
    {
        foreach (var slot in _slots)
        {
            if (slot.gameObject.activeSelf)
            {
                slot.RefreshStatus();
            }
        }
    }
}
