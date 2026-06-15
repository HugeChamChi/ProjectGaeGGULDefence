using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.UI.Totem
{
    /// <summary>
    /// 토템 범위 그리드의 개별 셀을 관리합니다.
    /// </summary>
    public class UI_TotemRangeCell : MonoBehaviour
    {
        [SerializeField] private Image img_Background;

        public void SetSprite(Sprite sprite)
        {
            if (img_Background == null) img_Background = GetComponent<Image>();
            if (img_Background != null)
            {
                img_Background.sprite = sprite;
                img_Background.color = Color.white; // Ensure tint doesn't hide sprite
            }
        }
    }
}
