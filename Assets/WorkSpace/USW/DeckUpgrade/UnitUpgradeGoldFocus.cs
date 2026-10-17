using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Dims the lobby chrome around an open unit popup while keeping its existing gold display visible.</summary>
    public sealed class UnitUpgradeGoldFocus : MonoBehaviour
    {
        [SerializeField] private RectTransform _goldCard;
        [SerializeField] private BottomPanelController _lobbyTabs;
        [SerializeField] private Sprite _rounded;
        private RectTransform _popup, _root, _highlight;
        private readonly RectTransform[] _shades = new RectTransform[5];
        private readonly Vector3[] _corners = new Vector3[4];
        private Vector2 _lastSize;
        private DroneUpgradeConceptScreen _screen;

        private void Start()
        {
            if (_goldCard == null || _lobbyTabs == null) return;
            _screen = GetComponent<DroneUpgradeConceptScreen>();
            if (_screen == null) return;
            var canvas = _goldCard.GetComponentInParent<Canvas>().rootCanvas;
            _root = new GameObject("UnitUpgradeGoldFocus", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.gameObject.hideFlags = HideFlags.DontSave;
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one; _root.offsetMin = _root.offsetMax = Vector2.zero;
            for (int i = 0; i < _shades.Length; i++)
                _shades[i] = Box("ChromeDim_" + i, new Color(0, 0, 0, .62f));
            _highlight = Box("GoldHighlight", new Color(1f, .78f, .30f, .55f));
            var image = _highlight.GetComponent<Image>(); image.sprite = _rounded; image.type = Image.Type.Sliced; image.fillCenter = false;
            _root.gameObject.SetActive(false);
        }

        private RectTransform Box(string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(_root, false); image.color = color; image.raycastTarget = true;
            return image.rectTransform;
        }

        private void LateUpdate()
        {
            if (_root == null) return;
            _popup = _screen.DetailsPopup;
            bool visible = _screen.isActiveAndEnabled && _popup != null && _popup.gameObject.activeInHierarchy && !_lobbyTabs.IsTransitioning;
            bool changed = _root.gameObject.activeSelf != visible;
            _root.gameObject.SetActive(visible);
            if (!visible || (!changed && _lastSize == _root.rect.size)) return;
            _lastSize = _root.rect.size;
            var bounds = _root.rect;
            _popup.GetWorldCorners(_corners);
            float pageBottom = _root.InverseTransformPoint(_corners[0]).y;
            float pageTop = _root.InverseTransformPoint(_corners[2]).y;
            _goldCard.GetWorldCorners(_corners);
            var goldMin = _root.InverseTransformPoint(_corners[0]);
            var goldMax = _root.InverseTransformPoint(_corners[2]);
            SetBounds(_shades[0], bounds.xMin, goldMax.y, bounds.xMax, bounds.yMax);
            SetBounds(_shades[1], bounds.xMin, pageTop, goldMin.x, goldMax.y);
            SetBounds(_shades[2], goldMax.x, pageTop, bounds.xMax, goldMax.y);
            SetBounds(_shades[3], goldMin.x, pageTop, goldMax.x, goldMin.y);
            SetBounds(_shades[4], bounds.xMin, bounds.yMin, bounds.xMax, pageBottom);
            SetBounds(_highlight, goldMin.x - 3, goldMin.y - 3, goldMax.x + 3, goldMax.y + 3);
            _root.SetAsLastSibling();
        }

        private void SetBounds(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(left - _root.rect.xMin, bottom - _root.rect.yMin);
            rect.sizeDelta = new Vector2(Mathf.Max(0, right - left), Mathf.Max(0, top - bottom));
        }

        private void OnDisable() { if (_root != null) _root.gameObject.SetActive(false); }
        private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
    }
}
