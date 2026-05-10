using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 리스트형 UI를 관리하는 베이스 클래스 (인스펙터 직렬화용)
/// </summary>
[System.Serializable]
public abstract class UI_ListBase<TData, TSlot> where TSlot : UI_SlotBase<TData>
{
    [Header("Hierarchy Control")]
    public GameObject rootObject; 

    [Header("Settings")]
    [SerializeField] protected TSlot slotPrefab;
    [SerializeField] protected Transform contentParent;

    protected List<TSlot> _activeSlots = new List<TSlot>();

    /// <summary>
    /// 데이터를 기반으로 리스트를 렌더링합니다.
    /// onBind 콜백을 통해 Presenter에서 슬롯의 상세 로직(이벤트 등)을 주입할 수 있습니다.
    /// </summary>
    public virtual void Render(IEnumerable<TData> dataList, Action<TData, TSlot> onBind = null)
    {
        if (slotPrefab == null || contentParent == null) return;

        int index = 0;
        foreach (var data in dataList)
        {
            TSlot slot = GetOrCreateSlot(index);
            slot.gameObject.SetActive(true);

            // 1. 기본 데이터 세팅
            slot.SetData(data);
            
            // 2. 외부(Presenter)에서의 추가 바인딩 로직 수행
            onBind?.Invoke(data, slot);

            index++;
        }

        // 사용하지 않는 남은 슬롯들은 비활성화 (Pooling 유지)
        for (int i = index; i < _activeSlots.Count; i++)
        {
            if (_activeSlots[i].gameObject.activeSelf)
                _activeSlots[i].gameObject.SetActive(false);
        }
    }

    protected TSlot GetOrCreateSlot(int index)
    {
        if (index < _activeSlots.Count) return _activeSlots[index];

        // RM 시스템을 통한 전역 풀링 활용
        TSlot slot = RM.Instantiate(slotPrefab.gameObject, contentParent, isPool: true).GetComponent<TSlot>();
        _activeSlots.Add(slot);
        return slot;
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

    public void SetActive(bool isActive)
    {
        if (rootObject != null) rootObject.SetActive(isActive);
    }
}
