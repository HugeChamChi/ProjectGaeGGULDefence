using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Expands the HUD menu above its button without changing combat or selection state.</summary>
public sealed class IngameHudMenu : MonoBehaviour
{
    [SerializeField] private Button _menuButton;
    [SerializeField] private Image _closedIcon;
    [SerializeField] private Image _openIcon;
    [SerializeField] private Button _dismissButton;
    [SerializeField] private Image _backdrop;
    [SerializeField, Range(0f, 1f)] private float _backdropOpacity = 1f;
    [SerializeField] private Vector2 _backdropPadding = new Vector2(12f, 12f);
    [SerializeField] private Button _selectionButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _damageButton;
    [SerializeField] private UI_Base _settingsPanel;
    [SerializeField] private CanvasGroup[] _items;
    [SerializeField, Min(0.01f)] private float _animationSeconds = 0.24f;
    [SerializeField, Min(1f)] private float _itemSpacing = 92f;
    [SerializeField] private UnityEvent _onSelectionListRequested = new UnityEvent();
    [SerializeField] private UnityEvent _onDamageChartRequested = new UnityEvent();

    private RectTransform[] _itemRects;
    private RectTransform _backdropRect;
    private RectTransform _backdropParent;
    private RectTransform _menuRect;
    private Tween _motion;
    private float _openness;
    private bool _expanded;

    /// <summary>Whether the menu has been requested open, including during its transition.</summary>
    public bool IsExpanded => _expanded;
    /// <summary>Connection point for the future acquired-selection list.</summary>
    public UnityEvent SelectionListRequested => _onSelectionListRequested;
    /// <summary>Connection point for the future damage chart.</summary>
    public UnityEvent DamageChartRequested => _onDamageChartRequested;

    private void Awake()
    {
        _itemRects = new RectTransform[_items.Length];
        _menuRect = (RectTransform)_menuButton.transform;
        if (_backdrop != null)
        {
            _backdropRect = _backdrop.rectTransform;
            _backdropParent = (RectTransform)_backdropRect.parent;
        }
        for (int i = 0; i < _items.Length; i++) _itemRects[i] = (RectTransform)_items[i].transform;
        _menuButton.onClick.AddListener(Toggle);
        _dismissButton.onClick.AddListener(Close);
        _selectionButton.onClick.AddListener(RequestSelectionList);
        _settingsButton.onClick.AddListener(OpenSettings);
        _damageButton.onClick.AddListener(RequestDamageChart);
        ApplyProgress(0f);
        _dismissButton.gameObject.SetActive(false);
    }

    /// <summary>Opens or closes the menu, reversing smoothly from its current position.</summary>
    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        _motion?.Kill();
        // Keep the art alive through fade-out, but release its input shield immediately.
        _dismissButton.gameObject.SetActive(true);
        _dismissButton.interactable = expanded;
        _dismissButton.image.raycastTarget = expanded;
        float target = expanded ? 1f : 0f;
        _motion = DOTween.To(() => _openness, ApplyProgress, target, _animationSeconds)
            .SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { if (!_expanded) _dismissButton.gameObject.SetActive(false); });
    }

    private void Toggle() => SetExpanded(!_expanded);
    private void Close() => SetExpanded(false);
    private void RequestSelectionList() { Close(); _onSelectionListRequested.Invoke(); }
    private void RequestDamageChart() { Close(); _onDamageChartRequested.Invoke(); }
    private void OpenSettings() { Close(); if (_settingsPanel != null) _settingsPanel.Open(); }

    private void ApplyProgress(float value)
    {
        _openness = value;
        for (int i = 0; i < _items.Length; i++)
        {
            // The item nearest the button unfolds first; all items return to the same origin.
            float progress = Mathf.Clamp01(value * 1.25f - (_items.Length - 1 - i) * .125f);
            var group = _items[i];
            group.alpha = Mathf.SmoothStep(0f, 1f, progress);
            group.interactable = _expanded && progress > .95f;
            group.blocksRaycasts = group.interactable;
            _itemRects[i].anchoredPosition = new Vector2(0f, (_items.Length - i) * _itemSpacing * progress);
            _itemRects[i].localScale = Vector3.one * Mathf.Lerp(.78f, 1f, progress);
        }
        var closed = _closedIcon.color; closed.a = 1f - value; _closedIcon.color = closed;
        var opened = _openIcon.color; opened.a = value; _openIcon.color = opened;
        _openIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(180f, 0f, value));
        if (_backdrop != null)
        {
            var color = _backdrop.color;
            color.a = _backdropOpacity * Mathf.SmoothStep(0f, 1f, value);
            _backdrop.color = color;
            RefreshBackdrop();
        }
    }

    private void LateUpdate()
    {
        if (_openness > 0f && _backdropRect != null) RefreshBackdrop();
    }

    private void RefreshBackdrop()
    {
        // Bound the art to the menu stack, including its arrow, independently of screen aspect.
        float width = _menuRect.rect.width;
        foreach (var item in _itemRects) width = Mathf.Max(width, item.rect.width);
        var center = _menuRect.rect.center;
        var lower = _backdropParent.InverseTransformPoint(_menuRect.TransformPoint(
            new Vector2(center.x, _menuRect.rect.yMin - _backdropPadding.y)));
        var upper = _backdropParent.InverseTransformPoint(_menuRect.TransformPoint(
            new Vector2(center.x, _menuRect.rect.yMax + _items.Length * _itemSpacing * _openness + _backdropPadding.y)));
        var left = _backdropParent.InverseTransformPoint(_menuRect.TransformPoint(
            new Vector2(center.x - width * .5f - _backdropPadding.x, center.y)));
        var right = _backdropParent.InverseTransformPoint(_menuRect.TransformPoint(
            new Vector2(center.x + width * .5f + _backdropPadding.x, center.y)));
        _backdropRect.localPosition = lower;
        _backdropRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, right.x - left.x);
        _backdropRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, upper.y - lower.y);
    }

    private void OnDisable()
    {
        _motion?.Kill();
        _expanded = false;
        if (_itemRects == null) return;
        ApplyProgress(0f);
        if (_dismissButton != null) _dismissButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _motion?.Kill();
        if (_menuButton != null) _menuButton.onClick.RemoveListener(Toggle);
        if (_dismissButton != null) _dismissButton.onClick.RemoveListener(Close);
        if (_selectionButton != null) _selectionButton.onClick.RemoveListener(RequestSelectionList);
        if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OpenSettings);
        if (_damageButton != null) _damageButton.onClick.RemoveListener(RequestDamageChart);
    }
}
