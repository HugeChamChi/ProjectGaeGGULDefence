using UnityEngine;
namespace GaeGGUL.DeckUpgrade
{
    public sealed class DeckUpgradeCrownFloat : MonoBehaviour
    {
        private float elapsed;
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            ((RectTransform)transform).anchoredPosition = new Vector2(0, Mathf.Sin(elapsed * Mathf.PI * 2 / 3.8f) * 6);
        }
    }
}
