using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Dims lobby chrome and shows upgrade tokens in the upper-right diamond card's position.</summary>
    public sealed class UnitUpgradeGoldFocus : MonoBehaviour
    {
        [SerializeField] private RectTransform _goldCard;
        [SerializeField] private BottomPanelController _lobbyTabs;
        private RectTransform _popup, _root, _tokenCard, _balanceCard;
        private TMP_Text _tokenAmount;
        private readonly RectTransform[] _shades = new RectTransform[2];
        private readonly List<(RectTransform Source, RectTransform Copy)> _visualRects = new();
        private readonly Vector3[] _corners = new Vector3[4];
        private Vector2 _lastSize;
        private DroneUpgradeConceptScreen _screen;

        private void Start()
        {
            if (_goldCard == null || _lobbyTabs == null) return;
            _screen = GetComponent<DroneUpgradeConceptScreen>();
            if (_screen == null) return;
            _balanceCard = _goldCard.parent.Find("DiamondCard") as RectTransform;
            if (_balanceCard == null) return;
            var canvas = _goldCard.GetComponentInParent<Canvas>().rootCanvas;
            _root = new GameObject("UnitUpgradeTokenBalance", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.gameObject.hideFlags = HideFlags.DontSave;
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one; _root.offsetMin = _root.offsetMax = Vector2.zero;
            for (int i = 0; i < _shades.Length; i++)
            {
                _shades[i] = Box("ChromeDim_" + i, new Color(0, 0, 0, .62f));
                var button = _shades[i].gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(_screen.CloseDetails);
            }
            _tokenCard = CopyVisual(_balanceCard, _root);
            _tokenCard.name = "UpgradeTokenCard";
            if (_tokenCard.GetComponent<Image>() == null)
            {
                var blocker = _tokenCard.gameObject.AddComponent<Image>(); blocker.color = Color.clear;
            }
            _tokenCard.GetComponent<Image>().raycastTarget = true;
            _root.gameObject.SetActive(false);
        }

        private RectTransform CopyVisual(RectTransform source, Transform parent)
        {
            var copy = new GameObject(source.name, typeof(RectTransform)).GetComponent<RectTransform>();
            copy.SetParent(parent, false); CopyRect(source, copy); _visualRects.Add((source, copy));
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            if (source.TryGetComponent<Image>(out var image))
            {
                var target = copy.gameObject.AddComponent<Image>();
                target.sprite = image.sprite; target.color = image.color; target.type = image.type;
                target.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier; target.preserveAspect = image.preserveAspect;
                target.raycastTarget = false;
                if (source.name.Contains("DiamondIcon")) target.sprite = DeckUpgradeCurrency.Icon;
            }
            if (source.TryGetComponent<TMP_Text>(out var text))
            {
                var target = copy.gameObject.AddComponent<TextMeshProUGUI>();
                target.font = text.font; target.fontSharedMaterial = text.fontSharedMaterial;
                target.fontSize = text.fontSize; target.fontStyle = text.fontStyle; target.color = text.color;
                target.alignment = text.alignment; target.margin = text.margin; target.text = text.text;
                target.enableAutoSizing = text.enableAutoSizing; target.fontSizeMin = text.fontSizeMin; target.fontSizeMax = text.fontSizeMax;
                target.textWrappingMode = text.textWrappingMode; target.raycastTarget = false;
                if (source.name.Contains("DiamondText")) _tokenAmount = target;
            }
            foreach (RectTransform child in source) CopyVisual(child, copy);
            return copy;
        }

        private static void CopyRect(RectTransform source, RectTransform copy)
        {
            copy.anchorMin = source.anchorMin; copy.anchorMax = source.anchorMax; copy.pivot = source.pivot;
            copy.sizeDelta = source.sizeDelta; copy.anchoredPosition = source.anchoredPosition;
            copy.localScale = source.localScale; copy.localRotation = source.localRotation;
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
            if (!visible) return;
            _tokenAmount.text = Player.PlayerData.IsReady ? DeckUpgradeCurrency.Balance(Player.PlayerData.Data, DeckUpgradeCurrency.Token).ToString("N0") : "—";
            foreach (var pair in _visualRects) if (pair.Copy != _tokenCard) CopyRect(pair.Source, pair.Copy);
            if (!changed && _lastSize == _root.rect.size) return;
            _lastSize = _root.rect.size;
            var bounds = _root.rect;
            _popup.GetWorldCorners(_corners);
            float pageBottom = _root.InverseTransformPoint(_corners[0]).y;
            float pageTop = _root.InverseTransformPoint(_corners[2]).y;
            _balanceCard.GetWorldCorners(_corners);
            var goldMin = _root.InverseTransformPoint(_corners[0]);
            var goldMax = _root.InverseTransformPoint(_corners[2]);
            SetBounds(_shades[0], bounds.xMin, pageTop, bounds.xMax, bounds.yMax);
            SetBounds(_shades[1], bounds.xMin, bounds.yMin, bounds.xMax, pageBottom);
            SetBounds(_tokenCard, goldMin.x, goldMin.y, goldMax.x, goldMax.y);
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
