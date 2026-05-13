using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitStatSlot : MonoBehaviour
    {
        [SerializeField] private Image           img_BG;
        [SerializeField] private Image           img_Icon;
        [SerializeField] private TextMeshProUGUI txt_Value;
        [SerializeField] private TextMeshProUGUI txt_BonusValue;

        public void Setup(UnitStatVisualInfo visual, string value, string bonusValue = "")
        {
            if (visual == null) return;

            if (img_BG != null)    img_BG.color = visual.bgColor;
            if (img_Icon != null)  img_Icon.sprite = visual.icon;
            if (txt_Value != null) txt_Value.text = value;

            if (txt_BonusValue != null)
            {
                bool hasBonus = !string.IsNullOrEmpty(bonusValue) && bonusValue != "0" && bonusValue != "+0" && bonusValue != "-0";
                txt_BonusValue.gameObject.SetActive(hasBonus);
                if (hasBonus) txt_BonusValue.text = bonusValue;
            }
        }
    }
}
