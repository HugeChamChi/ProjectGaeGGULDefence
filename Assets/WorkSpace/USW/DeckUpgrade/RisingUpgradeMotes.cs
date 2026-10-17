using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    public sealed class RisingUpgradeMotes : MonoBehaviour
    {
        private readonly RectTransform[] dots = new RectTransform[9];
        private readonly Image[] images = new Image[9];
        private float elapsed;

        public void Build(Sprite sprite)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                var go = new GameObject("RisingMote_" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                dots[i] = (RectTransform)go.transform;
                dots[i].anchorMin = dots[i].anchorMax = new Vector2(.5f, .5f);
                dots[i].sizeDelta = Vector2.one * (5 + i % 3 * 3);
                images[i] = go.GetComponent<Image>(); images[i].sprite = sprite; images[i].raycastTarget = false;
            }
            Sample();
        }

        private void Update() { elapsed += Time.unscaledDeltaTime; Sample(); }

        private void Sample()
        {
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                float t = Mathf.Repeat(elapsed / 5.5f + i * .117f, 1);
                float x = (i % 2 == 0 ? -1 : 1) * (145 + i % 4 * 31) + Mathf.Sin(t * 5 + i) * 12;
                dots[i].anchoredPosition = new Vector2(x, Mathf.Lerp(-135, 180, t));
                images[i].color = new Color(1, .87f, .55f, Mathf.Sin(t * Mathf.PI) * .65f);
            }
        }
    }
}
