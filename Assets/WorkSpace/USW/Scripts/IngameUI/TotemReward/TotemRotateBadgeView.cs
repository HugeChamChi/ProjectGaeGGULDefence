using UnityEngine;
using UnityEngine.UI;

/// <summary>A persistent, non-interactive badge owned by its icon or range box.</summary>
public sealed class TotemRotateBadgeView : MonoBehaviour
{
    private RectTransform _rect;
    private RectTransform _target;
    private Transform _canvas;
    private TotemRotateBadgeSettings _settings;
    private float _diameter;
    private Image _ring;
    private Image _background;
    private Image _icon;
    private Image _slash;
    private Vector2 _comparisonOffset;

    /// <summary>Creates the badge once beneath its target; canvas scaling preserves the authored diameter.</summary>
    public static TotemRotateBadgeView Create(RectTransform target, TotemRotateBadgeSettings settings, float diameter)
    {
        if (target == null || settings == null) return null;
        var go = new GameObject("RotateBadge", typeof(RectTransform), typeof(LayoutElement));
        go.layer = target.gameObject.layer;
        go.transform.SetParent(target, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var badge = go.AddComponent<TotemRotateBadgeView>();
        badge._rect = (RectTransform)go.transform;
        badge._rect.anchorMin = badge._rect.anchorMax = Vector2.one;
        badge._target = target;
        var canvas = target.GetComponentInParent<Canvas>();
        badge._canvas = canvas != null ? canvas.rootCanvas.transform : null;
        badge._settings = settings;
        badge._diameter = diameter;
        badge._rect.sizeDelta = Vector2.one * diameter;
        badge._ring = badge.AddImage("Ring", settings.Circle, Vector2.one * diameter);
        badge._background = badge.AddImage("Back", settings.Circle,
            Vector2.one * (diameter - Mathf.Max(settings.MinimumRingInset, diameter * settings.RingInsetRatio)));
        badge._background.color = settings.Background;
        badge._icon = badge.AddImage("Icon", settings.RotationIcon, Vector2.one * (diameter * settings.IconRatio));
        badge._slash = badge.AddImage("Slash", null, new Vector2(diameter * settings.SlashLengthRatio,
            Mathf.Max(settings.MinimumSlashThickness, diameter * settings.SlashThicknessRatio)));
        badge._slash.color = settings.SlashColor;
        badge._slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, settings.SlashAngle);
        badge.RefreshGeometry();
        go.SetActive(false);
        return badge;
    }

    /// <summary>Updates the existing badge from the same SO flag used by rotation gameplay.</summary>
    public void SetData(TotemData data)
    {
        bool show = _settings.ShouldShow(data);
        gameObject.SetActive(show);
        if (!show) return;
        _ring.color = data.isRotatable ? _settings.AvailableRing : _settings.UnavailableRing;
        _icon.color = data.isRotatable ? _settings.AvailableIcon : _settings.UnavailableIcon;
        _slash.gameObject.SetActive(!data.isRotatable);
        RefreshGeometry();
    }

    /// <summary>Creates a tutorial comparison beside this badge using the same artwork and authored totem flag.</summary>
    public TotemRotateBadgeView CreateComparison(TotemData data, float gap)
    {
        var comparison = Create(_target, _settings, _diameter);
        if (comparison == null) return null;
        comparison.name = "TutorialRotationComparison";
        comparison._comparisonOffset = Vector2.left * (_diameter + gap);
        comparison.SetData(data);
        return comparison;
    }

    private void LateUpdate() => RefreshGeometry();

    private void RefreshGeometry()
    {
        if (_target == null || _settings == null) return;
        var canvasScale = _canvas != null ? _canvas.lossyScale : Vector3.one;
        var targetScale = _target.lossyScale;
        if (Mathf.Approximately(targetScale.x, 0f) || Mathf.Approximately(targetScale.y, 0f)) return;
        var scale = new Vector3(Mathf.Abs(canvasScale.x / targetScale.x), Mathf.Abs(canvasScale.y / targetScale.y), 1f);
        _rect.localScale = scale;
        float inset = _diameter * 0.5f * _settings.CornerInset;
        _rect.anchoredPosition = Vector2.Scale(new Vector2(-inset, -inset) + _comparisonOffset, scale);
    }

    private Image AddImage(string name, Sprite sprite, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var image = go.GetComponent<Image>();
        image.rectTransform.sizeDelta = size;
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return image;
    }
}
