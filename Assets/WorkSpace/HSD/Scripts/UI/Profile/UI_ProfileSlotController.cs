using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UI_ProfileSlotController : MonoBehaviour
{
    [SerializeField] ScrollRect scroll;
    [SerializeField] UI_ProfileSlot slotPrefab;
    [SerializeField] List<UI_ProfileSlot> slots;
    [SerializeField] ProfileItemType type;

    public void Setup(Action<IProfileItem> onSelect = null)
    {
        if (slots != null && slots.Count > 0) return;

        var items = Table.Profile.GetItemByType(type);
        int count = items.Count();

        slots = new List<UI_ProfileSlot>(count);

        for (int i = 0; i < count; i++)
        {
            var slot = RM.Instantiate(slotPrefab, scroll.content);
            slot.SetData(items.ElementAt(i));
            slot.SetCallback(onSelect);
            slots.Add(slot);
        }
    }

    public void Refresh()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            slot.Refresh();
        }
    }
}
