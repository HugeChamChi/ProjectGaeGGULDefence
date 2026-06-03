using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HSD.InGameDebug
{
    public class UI_DebugTotemPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _txtContent;
        [SerializeField] private Button _btnClose;
        
        private void Awake()
        {
            if (_btnClose != null)
            {
                _btnClose.onClick.AddListener(() => Hide());
            }
        }

        public void Show(GridCellModel model, Vector2Int pos)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            var levelUpManager = FindObjectOfType<LevelUpManager>(true);
            float rowAtkMult = levelUpManager != null ? levelUpManager.GetRowAttackMultiplier(pos.y) : 1f;
            float rowSpdMult = levelUpManager != null ? levelUpManager.GetRowSpeedMultiplier(pos.y) : 1f;

            float rowAtkBonus = (rowAtkMult - 1f) * 100f;
            float rowSpdBonus = (rowSpdMult - 1f) * 100f;

            float totemAtkBonus = model.TotemCellAttackBonus * 100f;
            float totemSpdBonus = model.TotemCellSpeedBonus * 100f;

            float totalAtkBonus = totemAtkBonus + rowAtkBonus;
            float totalSpdBonus = totemSpdBonus + rowSpdBonus;

            string info = $"<color=yellow><b>[Cell {pos.x}, {pos.y}] Applied Cell Stats</b></color>\n\n";
            info += $"<color=#00ffff>Attack Bonus:</color> {totalAtkBonus:+0.##;-0.##;0}% <color=#aaaaaa>(Totem: {totemAtkBonus:+0.##;-0.##;0}%, Row: {rowAtkBonus:+0.##;-0.##;0}%)</color>\n";
            info += $"<color=#00ffff>Speed Bonus:</color> {totalSpdBonus:+0.##;-0.##;0}% <color=#aaaaaa>(Totem: {totemSpdBonus:+0.##;-0.##;0}%, Row: {rowSpdBonus:+0.##;-0.##;0}%)</color>\n";
            
            if (model.TotemCellFoodSpeedBonus != 0) info += $"<color=#00ffff>Food Speed Bonus:</color> {model.TotemCellFoodSpeedBonus * 100f:+0.##;-0.##;0}%\n";
            if (model.TotemCellFoodAmountBonus != 0) info += $"<color=#00ffff>Food Amount Bonus:</color> {model.TotemCellFoodAmountBonus * 100f:+0.##;-0.##;0}%\n";
            if (model.TotemCellCritChanceBonus != 0) info += $"<color=#00ffff>Crit Chance Bonus:</color> {model.TotemCellCritChanceBonus * 100f:+0.##;-0.##;0}%\n";
            if (model.TotemCellCritDamageBonus != 0) info += $"<color=#00ffff>Crit Damage Bonus:</color> {model.TotemCellCritDamageBonus * 100f:+0.##;-0.##;0}%\n";

            if (model.TotemAttackDisabled) info += $"\n<color=#ff0000>[Status] Attack Disabled by Totem</color>";
            if (model.TotemAttackModifier != 1f) info += $"\n<color=#ff00ff>Attack Multiplier:</color> x{model.TotemAttackModifier:0.##}";
            if (model.TotemSpeedModifier != 1f) info += $"\n<color=#ff00ff>Speed Multiplier:</color> x{model.TotemSpeedModifier:0.##}";
            if (model.NullifyDamageDebuff) info += $"\n<color=#00ff00>Nullify Boss Damage Debuff: ON</color>";

            _txtContent.text = info;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_btnClose != null) _btnClose.onClick.RemoveAllListeners();
        }
    }
}
