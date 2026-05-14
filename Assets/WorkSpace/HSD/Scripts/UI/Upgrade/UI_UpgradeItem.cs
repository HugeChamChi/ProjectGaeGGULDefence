using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI.Upgrade
{
    public class UI_UpgradeItem : MonoBehaviour
    {
        [SerializeField] private Image img_Icon;
        [SerializeField] private TextMeshProUGUI txt_Name;
        [SerializeField] private TextMeshProUGUI txt_Level;
        [SerializeField] private TextMeshProUGUI txt_Cost;
        [SerializeField] private Button btn_Upgrade;
        [SerializeField] private Image img_CostIcon;

        private string _target;
        private Action<string> _onUpgradeClicked;

        private void Awake()
        {
            if (btn_Upgrade != null)
            {
                btn_Upgrade.onClick.AddListener(() => _onUpgradeClicked?.Invoke(_target));
            }
        }

        public void Init(UpgradeModel.UpgradeItemData data, Action<string> onUpgradeClicked)
        {
            _target = data.UpgradeTarget;
            _onUpgradeClicked = onUpgradeClicked;
            
            UpdateUI(data);
        }

        public void UpdateUI(UpgradeModel.UpgradeItemData data)
        {
            if (txt_Name != null) txt_Name.text = data.DisplayName;
            if (txt_Level != null) txt_Level.text = $"Lv.{data.CurrentLevel}";
            
            if (data.IsMaxLevel)
            {
                if (txt_Cost != null) txt_Cost.text = "MAX";
                if (btn_Upgrade != null) btn_Upgrade.interactable = false;
            }
            else
            {
                if (txt_Cost != null) txt_Cost.text = data.UpgradeCost.ToString("N0");
                if (btn_Upgrade != null) btn_Upgrade.interactable = true;
            }

            if (img_Icon != null && data.Icon != null)
            {
                img_Icon.sprite = data.Icon;
            }
        }

        public void SetUpgradeInteractable(bool interactable)
        {
            if (btn_Upgrade != null)
            {
                // We should only enable if not Max level
                // But the presenter will handle this logic usually.
                btn_Upgrade.interactable = interactable;
            }
        }
    }
}
