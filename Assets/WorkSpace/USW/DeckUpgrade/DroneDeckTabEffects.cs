using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    public sealed class DroneDeckTabEffects : MonoBehaviour
    {
        private readonly RectTransform[] drones = new RectTransform[4];
        private static readonly Vector2[] Positions = { new Vector2(130, -65), new Vector2(260, -110), new Vector2(650, -110), new Vector2(780, -65) };
        private float elapsed;

        public void Build(Sprite[] sprites)
        {
            for (int i = 0; i < 4; i++)
            {
                var portrait = Create("TabDrone_" + i, Positions[i].x, -Positions[i].y, 100, 104);
                portrait.sprite = sprites[i]; portrait.preserveAspect = true; drones[i] = portrait.rectTransform;
            }
        }

        private Image Create(string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height);
            var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < 4; i++)
            {
                if (drones[i] == null) continue;
                float bob = Mathf.Sin(elapsed * 2 + i) * 14;
                drones[i].anchoredPosition = Positions[i] + new Vector2(Mathf.Sin(elapsed * 1.4f + i) * 18, bob);
                drones[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 1.4f + i) * 5);

            }
        }
    }
}
