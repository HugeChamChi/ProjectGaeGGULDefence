using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GaeGGUL.UI.Common
{
    /// <summary>
    /// 아이콘, 배경색, 수치를 표시하는 범용 스텟 슬롯 컴포넌트입니다.
    /// </summary>
    public class UI_StatSlot : MonoBehaviour
    {
        [SerializeField] private Image           img_BG;
        [SerializeField] private Image           img_Icon;
        [SerializeField] private TextMeshProUGUI txt_Value;
        [SerializeField] private TextMeshProUGUI txt_BonusValue;

        /// <summary>
        /// 슬롯의 정보를 설정합니다.
        /// </summary>
        public void Setup(Sprite icon, Color bgColor, string value, string bonusValue = "")
        {
            if (img_BG != null)    img_BG.color = bgColor;
            if (img_Icon != null)
            {
                img_Icon.sprite = icon;
                img_Icon.gameObject.SetActive(icon != null);
            }
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
