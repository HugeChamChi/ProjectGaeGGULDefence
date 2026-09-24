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
                InGameLifetimeScope.GlobalResolver?.Inject(upgradeModel);
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

        public void SetCurrency(float gold)
        {
            if (txt_Gold != null) txt_Gold.text = gold.ToString("N0");
        }

        /// <summary>displayConfigs 구성이 바뀌어도(항목 추가/삭제) 맞춰서 아이템을 생성/삭제한다.
        /// 이미 있는 타겟은 새로 만들지 않고 데이터만 갱신한다.</summary>
        public void InitItems(List<UpgradeModel.UpgradeItemData> dataList, Action<string> onUpgradeClicked)
        {
            var currentKeys = new HashSet<string>();
            foreach (var data in dataList) currentKeys.Add(data.UpgradeTarget);

            var toRemove = new List<string>();
            foreach (var kv in _items)
                if (!currentKeys.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var key in toRemove)
            {
                if (_items[key] != null) Destroy(_items[key].gameObject);
                _items.Remove(key);
            }

            foreach (var data in dataList)
            {
                if (_items.TryGetValue(data.UpgradeTarget, out var existing))
                {
                    existing.UpdateUI(data);
                    continue;
                }

                // RM.Instantiate를 사용하는 것이 원칙이나, 프리팹 참조가 직접 연결된 경우 대응
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

        /// <summary>해당 카드에 강화 결과 연출을 재생한다.</summary>
        public void PlayUpgradeResult(string target, bool success)
        {
            if (!_items.TryGetValue(target, out var item) || item == null) return;
            if (success) item.PlayUpgradeSuccess();
            else item.PlayUpgradeRejected();
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
