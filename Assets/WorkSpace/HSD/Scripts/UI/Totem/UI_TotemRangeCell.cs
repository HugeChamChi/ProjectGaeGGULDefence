using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.UI.Totem
{
    /// <summary>
    /// 토템 범위 그리드의 개별 셀. 토템 칸은 꽉 채우고, 범위 칸은 반투명 채움 + 테두리로 그린다.
    /// 테두리 Image는 프리팹에 없으면 런타임에 자식으로 만든다.
    /// </summary>
    public class UI_TotemRangeCell : MonoBehaviour
    {
        [SerializeField] private Image img_Background;
        [SerializeField] private Image img_Outline;

        /// <summary>토템 자신의 칸: 단색으로 꽉 채우고 테두리는 숨긴다.</summary>
        public void SetTotem(Color color)
        {
            var bg = Background();
            if (bg != null)
            {
                bg.sprite = null;
                bg.color = color;
            }
            if (img_Outline != null) img_Outline.enabled = false;
        }

        /// <summary>범위 칸: 안쪽은 반투명 채움, 바깥은 9-slice 테두리.</summary>
        public void SetRange(Color fill, Color outline, Sprite outlineSprite, float thicknessMultiplier)
        {
            var bg = Background();
            if (bg != null)
            {
                bg.sprite = null;
                bg.color = fill;
            }
            var ol = Outline();
            if (ol == null) return;
            ol.sprite = outlineSprite;
            ol.type = Image.Type.Sliced;
            ol.fillCenter = false;
            ol.pixelsPerUnitMultiplier = thicknessMultiplier;
            ol.color = outline;
            ol.enabled = outlineSprite != null;
        }

        private Image Background()
        {
            if (img_Background == null) img_Background = GetComponent<Image>();
            if (img_Background != null) img_Background.raycastTarget = false;
            return img_Background;
        }

        private Image Outline()
        {
            if (img_Outline != null) return img_Outline;
            var go = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            img_Outline = go.GetComponent<Image>();
            img_Outline.raycastTarget = false;
            return img_Outline;
        }
    }
}
