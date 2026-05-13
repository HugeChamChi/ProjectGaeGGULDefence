using UnityEngine;
using UnityEngine.UI;
using GaeGGUL.Extension;

namespace GaeGGUL.UI.Common
{
    /// <summary>
    /// 등급(Tier)에 따라 아이콘, 테두리, 배경색을 관리하는 전용 UI 컴포넌트입니다.
    /// </summary>
    public class UI_IconTierSlot : MonoBehaviour
    {
        [SerializeField] private Image img_Icon;
        [SerializeField] private Image img_Frame;
        [SerializeField] private Image img_BG;

        /// <summary>
        /// 아이콘과 등급 정보를 바탕으로 비주얼을 설정합니다.
        /// </summary>
        public void SetData(Sprite icon, Tier tier)
        {
            if (img_Icon != null)
            {
                img_Icon.sprite = icon;
                img_Icon.gameObject.SetActive(icon != null);
            }

            if (img_Frame != null)
            {
                img_Frame.sprite = tier.GetFrame();
                img_Frame.gameObject.SetActive(img_Frame.sprite != null);
            }

            if (img_BG != null)
            {
                img_BG.color = tier.GetBGColor();
            }
        }

        /// <summary>
        /// 단순 아이콘 스프라이트만 교체합니다.
        /// </summary>
        public void SetIcon(Sprite icon)
        {
            if (img_Icon != null) img_Icon.sprite = icon;
        }
    }
}
