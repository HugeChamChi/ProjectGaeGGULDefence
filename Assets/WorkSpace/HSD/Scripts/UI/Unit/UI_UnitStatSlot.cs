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

        public void Setup(UnitStatVisualInfo visual, string value)
        {
            if (visual == null) return;

            if (img_BG != null)    img_BG.color = visual.bgColor;
            if (img_Icon != null)  img_Icon.sprite = visual.icon;
            if (txt_Value != null) txt_Value.text = value;
        }
    }
}
