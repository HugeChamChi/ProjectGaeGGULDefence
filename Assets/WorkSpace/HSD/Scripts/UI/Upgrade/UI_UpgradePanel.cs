using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HSD.UI.Upgrade
{
    public class UI_UpgradePanel : UI_Base
    {
        [Header("Currency")]
        [SerializeField] private TextMeshProUGUI txt_Gold;
        [SerializeField] private TextMeshProUGUI txt_Silver; // If applicable

        [Header("Upgrade List")]
        [SerializeField] private Transform itemContainer;
        [SerializeField] private UI_UpgradeItem itemPrefab;
        [SerializeField] private UpgradeModel upgradeModel;

        private Dictionary<string, UI_UpgradeItem> _items = new();
        private UpgradePresenter _presenter;

        protected override void Awake()
        {
            base.Awake();
            
            if (upgradeModel != null)
            {
                upgradeModel.Initialize();
                _presenter = new UpgradePresenter(upgradeModel, this);
            }
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            if (upgradeModel != null)
            {
                upgradeModel.Release();
            }
        }

        public void SetCurrency(float gold, float silver = 0)
        {
            if (txt_Gold != null) txt_Gold.text = gold.ToString("N0");
            if (txt_Silver != null) txt_Silver.text = silver.ToString("N0");
        }

        public void InitItems(List<UpgradeModel.UpgradeItemData> dataList, Action<string> onUpgradeClicked)
        {
            // Clear existing if needed (though usually Init is called once)
            foreach (Transform child in itemContainer)
            {
                // RM.Destroy should be used if they were pooled, but for now we'll assume they are simple
                Destroy(child.gameObject);
            }
            _items.Clear();

            foreach (var data in dataList)
            {
                // In a real scenario, use RM.Instantiate if itemPrefab is a prefab path or object
                UI_UpgradeItem item = Instantiate(itemPrefab, itemContainer);
                item.Init(data, onUpgradeClicked);
                _items.Add(data.UpgradeTarget, item);
            }
        }

        public void UpdateItem(UpgradeModel.UpgradeItemData data)
        {
            if (_items.TryGetValue(data.UpgradeTarget, out var item))
            {
                item.UpdateUI(data);
            }
        }

        public void UpdateAllItems(List<UpgradeModel.UpgradeItemData> dataList)
        {
            foreach (var data in dataList)
            {
                UpdateItem(data);
            }
        }
    }
}
