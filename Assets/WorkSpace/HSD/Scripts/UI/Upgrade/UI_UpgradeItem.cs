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
        [Header("Upgrade Button States")]
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _pressedSprite;
        [SerializeField] private Sprite _maxSprite;

        private string _target;
        private Action<string> _onUpgradeClicked;
        private bool _canUpgrade;

        private void Awake()
        {
            if (btn_Upgrade != null)
            {
                btn_Upgrade.onClick.AddListener(() =>
                {
                    if (_canUpgrade && btn_Upgrade.interactable) _onUpgradeClicked?.Invoke(_target);
                });
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
            _canUpgrade = !data.IsMaxLevel && data.UpgradeCost >= 0;
            if (txt_Name != null) txt_Name.text = data.DisplayName;
            
            if (data.IsMaxLevel)
            {
                if (txt_Level != null) txt_Level.text = $"Lv.{data.CurrentLevel}";
                if (txt_Cost != null) txt_Cost.text = "MAX";
                if (btn_Upgrade != null) btn_Upgrade.interactable = false;
            }
            else
            {
                if (txt_Level != null) txt_Level.text = $"Lv.{data.CurrentLevel}";
                if (txt_Cost != null) txt_Cost.text = data.UpgradeCost >= 0 ? data.UpgradeCost.ToString("N0") : "—";
            }
            if (img_CostIcon != null) img_CostIcon.gameObject.SetActive(!data.IsMaxLevel);
            UpdateButton(data.IsMaxLevel);

            if (img_Icon != null && data.Icon != null)
            {
                img_Icon.sprite = data.Icon;
            }
        }

        public void SetUpgradeInteractable(bool interactable)
        {
            if (btn_Upgrade != null)
            {
                btn_Upgrade.interactable = interactable && _canUpgrade;
            }
        }

        private void UpdateButton(bool isMax)
        {
            if (btn_Upgrade == null) return;
            btn_Upgrade.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            var state = btn_Upgrade.spriteState;
            state.highlightedSprite = _normalSprite;
            state.selectedSprite = _normalSprite;
            state.pressedSprite = _pressedSprite;
            state.disabledSprite = isMax ? _maxSprite : _normalSprite;
            btn_Upgrade.spriteState = state;
            if (btn_Upgrade.targetGraphic is Image image)
            {
                image.sprite = isMax ? _maxSprite : _normalSprite;
                image.overrideSprite = null;
            }
            btn_Upgrade.interactable = _canUpgrade;
        }
    }
}
