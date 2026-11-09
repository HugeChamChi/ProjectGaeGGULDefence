using UnityEngine;
using UnityEngine.UI;

/// <summary>Temporary authored shop layout. Only section navigation is connected.</summary>
public sealed class LobbyShopPreview : MonoBehaviour
{
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _currencySection;
    [SerializeField] private GridLayoutGroup _currencyGrid;
    [SerializeField] private LayoutElement _gridHeight;
    [SerializeField] private Button _packageTab;
    [SerializeField] private Button _currencyTab;
    private static readonly Color Selected = new Color(.48f, .28f, .86f);
    private static readonly Color Unselected = new Color(.18f, .15f, .27f);

    private void Awake()
    {
        _packageTab.onClick.AddListener(ShowPackages);
        _currencyTab.onClick.AddListener(ShowCurrency);
        _scroll.onValueChanged.AddListener(OnScroll);
    }

    private void Start() => RefreshLayout();
    private void OnEnable() => RefreshLayout();
    private void OnRectTransformDimensionsChange() => RefreshLayout();

    private void RefreshLayout()
    {
        if (_currencyGrid == null || _scroll == null) return;
        // Measure the viewport, not the grid's preferred width: the layout group derives
        // that preferred width from cellSize and would feed it back into this calculation.
        float width = _scroll.viewport.rect.width - 48f;
        if (width <= 36f) return;
        float cell = (width - _currencyGrid.spacing.x * 2f) / 3f;
        _currencyGrid.cellSize = new Vector2(cell, 304f);
        _gridHeight.preferredHeight = 608f + _currencyGrid.spacing.y;
        OnScroll(Vector2.zero);
    }

    /// <summary>Shows the first package without purchasing or granting anything.</summary>
    public void ShowPackages()
    {
        _scroll.StopMovement();
        _scroll.verticalNormalizedPosition = 1f;
        Highlight(false);
    }

    /// <summary>Shows the temporary currency cards, including entry from header currency buttons.</summary>
    public void ShowCurrency()
    {
        Canvas.ForceUpdateCanvases();
        _scroll.StopMovement();
        float travel = _scroll.content.rect.height - _scroll.viewport.rect.height;
        float top = -_currencySection.anchoredPosition.y;
        _scroll.verticalNormalizedPosition = travel <= 0f ? 1f : 1f - Mathf.Clamp01(top / travel);
        Highlight(true);
    }

    private void OnScroll(Vector2 value)
    {
        if (_scroll == null || _currencySection == null) return;
        float top = -_currencySection.anchoredPosition.y;
        float travel = Mathf.Max(0f, _scroll.content.rect.height - _scroll.viewport.rect.height);
        float target = Mathf.Min(top, travel);
        Highlight(target > 0f && _scroll.content.anchoredPosition.y >= target - 4f);
    }

    private void Highlight(bool currency)
    {
        if (_packageTab != null) _packageTab.image.color = currency ? Unselected : Selected;
        if (_currencyTab != null) _currencyTab.image.color = currency ? Selected : Unselected;
    }

    private void OnDestroy()
    {
        _packageTab.onClick.RemoveListener(ShowPackages);
        _currencyTab.onClick.RemoveListener(ShowCurrency);
        _scroll.onValueChanged.RemoveListener(OnScroll);
    }
}
