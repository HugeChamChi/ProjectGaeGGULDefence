using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "변환" button floating above the selected unit. It is parented under the unit info panel so a tap on it
/// counts as inside the panel and does not dismiss the selection first. Built at runtime; no prefab.
/// </summary>
public sealed class ConversionButtonView : MonoBehaviour, IPointerClickHandler
{
    private const float Width = 250f;
    private const float Height = 92f;
    private const float IconSize = 54f;
    private const float FontSize = 40f;
    private const float AboveCell = 18f;
    private static readonly Color Fill = new Color(0.33f, 0.2f, 0.62f, 0.96f);
    private static readonly Color FillDisabled = new Color(0.24f, 0.22f, 0.3f, 0.92f);
    private static readonly Color CostShort = new Color(1f, 0.45f, 0.45f);

    private ConversionService _service;
    private ConversionStoneWallet _wallet;
    private Action<string> _warn;
    private RectTransform _rect;
    private Canvas _canvas;
    private Image _background;
    private TMP_Text _label, _cost;
    private CenterToastSprites _sprites;
    private UnitBase _unit;

    /// <summary>Unit the button currently converts, or null while hidden.</summary>
    public UnitBase Unit => _unit;

    /// <summary>Builds the hidden button under <paramref name="panel"/>.</summary>
    public static ConversionButtonView Create(RectTransform panel, ConversionService service, ConversionStoneWallet wallet,
        Sprite icon, TMP_FontAsset font, Action<string> warn)
    {
        var view = new GameObject("ConversionButton", typeof(RectTransform)).AddComponent<ConversionButtonView>();
        view._service = service; view._wallet = wallet; view._warn = warn;
        view._sprites = new CenterToastSprites();
        var rect = view._rect = (RectTransform)view.transform;
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(Width, Height);

        view._background = view.gameObject.AddComponent<Image>();
        view._background.sprite = view._sprites.Pill;
        view._background.type = Image.Type.Sliced;
        view._background.pixelsPerUnitMultiplier = view._sprites.PillBorder / (Height * 0.5f);
        view._background.color = Fill;
        var outline = view.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.06f, 0.26f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);

        view._label = Text(rect, "Label", font, "변환", new Vector2(-46f, 0f), 110f);
        var iconImage = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        iconImage.sprite = icon; iconImage.preserveAspect = true; iconImage.raycastTarget = false;
        var iconRect = iconImage.rectTransform;
        iconRect.SetParent(rect, false);
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        iconRect.anchoredPosition = new Vector2(38f, 0f);
        view._cost = Text(rect, "Cost", font, "1", new Vector2(88f, 0f), 50f);
        view.gameObject.SetActive(false);
        return view;
    }

    private static TMP_Text Text(RectTransform parent, string name, TMP_FontAsset font, string value, Vector2 position, float width)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var rect = text.rectTransform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(width, Height);
        rect.anchoredPosition = position;
        if (font != null) text.font = font;
        text.fontSize = FontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.color = Color.white;
        text.text = value;
        return text;
    }

    /// <summary>Shows the button for a selected unit; hides it when the unit cannot be converted at all.</summary>
    public void Show(UnitBase unit, bool canMerge)
    {
        if (!_service.Offers(unit)) { Hide(); return; }
        _unit = unit;
        _canvas = GetComponentInParent<Canvas>();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    /// <summary>Hides the button and forgets the unit.</summary>
    public void Hide()
    {
        _unit = null;
        if (this != null && gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_unit == null || !_unit.isActiveAndEnabled || !_service.Offers(_unit)) { Hide(); return; }
        Refresh();
        Place();
    }

    private void Refresh()
    {
        int cost = _service.Cost(_unit);
        bool ready = _service.CanConvert(_unit, out _);
        _cost.text = cost.ToString();
        _cost.color = _wallet.Stones >= cost ? Color.white : CostShort;
        _background.color = ready ? Fill : FillDisabled;
        _label.alpha = ready ? 1f : 0.7f;
    }

    private void Place()
    {
        var cam = Camera.main;
        var parent = _rect.parent as RectTransform;
        if (cam == null || parent == null || _unit.currentCell == null) return;
        Vector3 world = _unit.currentCell.TryGetVisualBounds(out var bounds)
            ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)
            : _unit.transform.position;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        var uiCam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out var local)) return;
        _rect.localPosition = new Vector3(local.x, local.y + AboveCell, 0f);
    }

    /// <summary>Converts on tap, or explains why not with a warning toast.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        var unit = _unit;
        if (unit == null) return;
        if (_service.TryConvert(unit, out string reason)) { Hide(); return; }
        _warn?.Invoke(reason);
    }

    private void OnDestroy() => _sprites?.Dispose();
}
