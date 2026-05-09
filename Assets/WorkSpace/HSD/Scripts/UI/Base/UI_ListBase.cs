using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 리스트형 UI를 관리하는 베이스 클래스 (인스펙터 직렬화용)
/// </summary>
[System.Serializable]
public abstract class UI_ListBase<TData, TSlot> where TSlot : UI_SlotBase<TData>
{
    [Header("Hierarchy Control")]
    public GameObject rootObject; // 기존의 gameObject.SetActive를 대신함

    [Header("Settings")]
    [SerializeField] protected TSlot slotPrefab;
    [SerializeField] protected Transform contentParent;

    protected List<TSlot> _activeSlots = new List<TSlot>();

    public virtual void Setup(IEnumerable<TData> dataList)
    {
        if (slotPrefab == null || contentParent == null) return;

        int index = 0;
        foreach (var data in dataList)
        {
            TSlot slot;
            if (index < _activeSlots.Count)
            {
                slot = _activeSlots[index];
                slot.gameObject.SetActive(true);
            }
            else
            {
                slot = RM.Instantiate(slotPrefab.gameObject, contentParent, isPool: true).GetComponent<TSlot>();
                _activeSlots.Add(slot);
            }

            slot.SetData(data);
            index++;
        }

        for (int i = index; i < _activeSlots.Count; i++)
        {
            if (_activeSlots[i].gameObject.activeSelf)
                _activeSlots[i].gameObject.SetActive(false);
        }
    }

    public void RefreshAll()
    {
        foreach (var slot in _activeSlots)
        {
            if (slot.gameObject.activeSelf)
                slot.SetData(slot.Data);
        }
    }

    public List<TSlot> GetActiveSlots() => _activeSlots.FindAll(s => s.gameObject.activeSelf);

    // 편의를 위한 래핑 함수
    public void SetActive(bool isActive)
    {
        if (rootObject != null) rootObject.SetActive(isActive);
    }
}
