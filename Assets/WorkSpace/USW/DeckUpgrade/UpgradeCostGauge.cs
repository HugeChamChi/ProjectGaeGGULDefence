using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Plain money gauge with a bouncing, shimmering readiness arrow.</summary>
    public sealed class UpgradeCostGauge : MonoBehaviour
    {
        public float Ratio { get; private set; }
        public bool Ready { get; private set; }
        public RectTransform Arrow { get; private set; }
        public Image Fill { get; private set; }
        private RectTransform fillRect, arrowReflection;
        private float clock, height;

        public static UpgradeCostGauge Create(RectTransform parent, Sprite rounded, Sprite arrowSprite)
        {
            var gauge = parent.gameObject.AddComponent<UpgradeCostGauge>();
            gauge.height = parent.sizeDelta.y;
            gauge.clock = parent.anchoredPosition.y * .002f;
            var mask = NewImage("GaugeClip", parent, Color.white, rounded); Stretch(mask.rectTransform);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            gauge.Fill = NewImage("GreenFill", mask.transform, new Color32(69, 184, 104, 255), null);
            gauge.fillRect = gauge.Fill.rectTransform; Stretch(gauge.fillRect);
            var arrow = NewImage("UpgradeReadyArrow", parent, new Color32(108, 255, 112, 255), arrowSprite);
            arrow.type = Image.Type.Simple; arrow.preserveAspect = true;
            gauge.Arrow = arrow.rectTransform;
            gauge.Arrow.anchorMin = gauge.Arrow.anchorMax = new Vector2(1, .5f);
            gauge.Arrow.sizeDelta = new Vector2(gauge.height * 1.84f, gauge.height * 2.36f);
            gauge.Arrow.anchoredPosition = new Vector2(8, 0);
            arrow.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            gauge.arrowReflection = NewImage("ArrowWhiteReflection", arrow.transform, new Color(1, 1, 1, .9f), null).rectTransform;
            gauge.arrowReflection.anchorMin = gauge.arrowReflection.anchorMax = new Vector2(.5f, 0);
            gauge.arrowReflection.sizeDelta = new Vector2(gauge.height * 3, gauge.height * .34f);
            gauge.arrowReflection.localRotation = Quaternion.Euler(0, 0, -18);
            gauge.SetAmount(0, 1, true);
            return gauge;
        }

        public void SetAmount(int available, int required, bool canUpgrade)
        {
            Ratio = Mathf.Clamp01(available / (float)required);
            Ready = canUpgrade && available >= required;
            fillRect.anchorMax = new Vector2(Ratio, 1);
            Arrow.gameObject.SetActive(Ready);
        }

        private void Update()
        {
            if (!Ready) return;
            clock += Time.unscaledDeltaTime;
            float bounce = Mathf.Sin(clock * 6);
            Arrow.anchoredPosition = new Vector2(8, Mathf.Max(0, bounce) * height * .08f);
            Arrow.localScale = new Vector3(1 - bounce * .025f, 1 + bounce * .08f, 1);
            float arrowSweep = Mathf.Repeat(clock + .25f, 1.2f) / .6f;
            arrowReflection.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(-height, height * 3, arrowSweep));
        }

        private static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color;
            image.type = Image.Type.Sliced; image.raycastTarget = false; return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
