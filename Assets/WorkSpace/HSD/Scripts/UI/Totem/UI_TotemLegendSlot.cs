using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GaeGGUL.UI.Totem
{
    /// <summary>
    /// 토템 범례(Legend)의 개별 항목을 관리하는 슬롯 클래스입니다.
    /// </summary>
    public class UI_TotemLegendSlot : MonoBehaviour
    {
        [SerializeField] private Image img_Color;
        [SerializeField] private TextMeshProUGUI txt_Label;

        public void SetData(string label, Color color)
        {
            if (txt_Label != null) txt_Label.text = label;
            if (img_Color != null) img_Color.color = color;
        }
    }
}
