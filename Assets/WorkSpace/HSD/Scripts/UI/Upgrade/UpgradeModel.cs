using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HSD.UI.Upgrade
{
    [CreateAssetMenu(fileName = "UpgradeModel", menuName = "UI/UpgradeModel")]
    public class UpgradeModel : ScriptableObject
    {
        [Serializable]
        public struct UpgradeDisplayConfig
        {
            public string targetKey;
            public string displayName;
            public Sprite icon;
        }

        public struct UpgradeItemData
        {
            public string UpgradeTarget;
            public string DisplayName;
            public Sprite Icon;
            public int CurrentLevel;
            public int UpgradeCost;
            public bool IsMaxLevel;
        }

        [Header("UI Configuration")]
        [SerializeField] private List<UpgradeDisplayConfig> displayConfigs;

        public event Action OnDataChanged;
        public event Action<float> OnCurrencyChanged;

        // SO는 인스펙터에서 설정되므로 런타임에 초기화가 필요할 수 있음
        public void Initialize()
        {
            if (Manager.Currency != null)
            {
                Manager.Currency.OnCurrencyChanged += HandleCurrencyChanged;
            }
        }

        public void Release()
        {
            if (Manager.Currency != null)
            {
                Manager.Currency.OnCurrencyChanged -= HandleCurrencyChanged;
            }
        }

        private void HandleCurrencyChanged(float amount)
        {
            OnCurrencyChanged?.Invoke(amount);
        }

        public List<UpgradeItemData> GetUpgradeItems()
        {
            var items = new List<UpgradeItemData>();
            foreach (var config in displayConfigs)
            {
                string target = config.targetKey;
                int currentLevel = Manager.Upgrade.GetUpgradeLevel(target);
                int cost = Manager.Upgrade.GetUpgradeCost(target);

                items.Add(new UpgradeItemData
                {
                    UpgradeTarget = target,
                    DisplayName = config.displayName,
                    Icon = config.icon,
                    CurrentLevel = currentLevel,
                    UpgradeCost = cost,
                    IsMaxLevel = cost < 0
                });
            }
            return items;
        }

        public float GetCurrentCurrency()
        {
            return Manager.Currency != null ? Manager.Currency.Currency : 0;
        }

        public bool TryUpgrade(string target)
        {
            bool success = Manager.Upgrade.TryUpgrade(target);
            if (success)
            {
                OnDataChanged?.Invoke();
            }
            return success;
        }
    }
}
