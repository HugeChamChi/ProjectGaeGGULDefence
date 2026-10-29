using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tier tag and skill charge under one field unit: a small dark tag with the tier number plus a quiet charge
/// display, drawn as one or two shader quads with one shared material.
/// Entry point is <see cref="Bind"/>; the owning overlay calls <see cref="Refresh"/> every frame.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UnitStatusBadge : MaskableGraphic
{
    // UV0.z primitive kinds, UV1.y states and UV2.y tag charge modes, read by GGD/UI/UnitStatusBadge.
    private const float KindTag = 1f, KindBar = 2f;
    private const float StateCharging = 0f, StatePassive = 1f, StateFull = 2f;
    private const float TagChargeNone = 0f, TagChargeBorder = 1f, TagChargeFill = 2f;
    private const float NeverFlashed = -1000f;
    // Quad padding beyond the shape so the outline is not clipped (shape units).
    private const float TagPad = 1.5f, BarPad = 1.4f;
    // UV2.z tag symbols beyond the numbers 1-4.
    private const float SymbolStar = 5f, SymbolStarOutline = 6f;

    [SerializeField] private UnitStatusBadgeSettings _settings;
    [Header("Prefab preview only")]
    [SerializeField] private Tier _previewTier = Tier.Legend;
    [SerializeField, Range(0f, 1f)] private float _previewProgress = 0.5f;
    [SerializeField] private bool _previewPassive;

    private Vector3 _authoredScale;
    private bool _bound;
    private DragHandler _dragHandler;
    private Tier _tier;
    private bool _passive;
    private bool _full;
    private float _progress;
    private float _targetProgress;
    private bool _hasProgress;
    private float _lastCharge;
    private bool _hasCharge;
    private float _flashStart = NeverFlashed;
    private Tweener _progressTween;

    /// <summary>Unit this badge follows.</summary>
    public UnitBase Unit { get; private set; }
    /// <summary>Displayed tier.</summary>
    public Tier DisplayedTier => _tier;
    /// <summary>Displayed (eased) charge 0-1.</summary>
    public float DisplayedProgress => _progress;
    /// <summary>Whether the unit has no skill charge and shows the disabled charge.</summary>
    public bool IsPassive => _passive;
    /// <summary>Whether the skill is fully charged and waiting.</summary>
    public bool IsFull => _full;
    /// <summary>Unscaled time of the last ready blink.</summary>
    public float LastFlashTime => _flashStart;

    /// <summary>Look and timing. Changing it relayouts this badge.</summary>
    public UnitStatusBadgeSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            ApplyFrame();
            SetVerticesDirty();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (canvas != null) canvas.additionalShaderChannels |= UnitStatusBadgeOverlay.RequiredChannels;
    }

    /// <summary>Connects a unit. Keeps the prefab's authored scale; position follows the unit's cell.</summary>
    public void Bind(UnitBase unit)
    {
        _progressTween?.Kill();
        _progressTween = null;
        _hasProgress = false;
        _hasCharge = false;
        _flashStart = NeverFlashed;
        Unit = unit;
        _dragHandler = unit != null ? unit.GetComponent<DragHandler>() : null;
        _bound = true;
        _authoredScale = rectTransform.localScale;
        raycastTarget = false;
        ApplyFrame();
        Refresh();
        SetVerticesDirty();
    }

    /// <summary>Applies placement, tier and charge. Hidden badges are still refreshed by the owner.</summary>
    public void Refresh()
    {
        bool visible = Unit != null && Unit.isActiveAndEnabled && Unit.currentCell != null && Unit.currentCell.OccupyingUnit == Unit;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (!visible || _settings == null) return;
        PositionWithinCell();
        bool passive = !Unit.HasSkillCooldownGauge;
        float charge = passive ? 0f : Unit.SkillGaugeProgress;
        if (float.IsNaN(charge) || float.IsInfinity(charge)) charge = 0f;
        charge = Mathf.Clamp01(charge);
        bool full = !passive && charge >= _settings.ReadyThreshold;
        if (_tier != Unit.currentTier || _passive != passive) _hasCharge = false;
        // Ready moment: reaching full, or a drop from near full when the skill fired between two frames.
        bool fired = _hasCharge && _lastCharge >= _settings.FiredFromCharge && charge < _lastCharge - 0.5f;
        if (!passive && _hasCharge && !_full && (full || fired))
        {
            _flashStart = Time.unscaledTime;
            SetVerticesDirty();
        }
        _lastCharge = charge;
        _hasCharge = true;
        bool stateChanged = _tier != Unit.currentTier || _passive != passive || _full != full;
        _tier = Unit.currentTier;
        _passive = passive;
        _full = full;
        AnimateProgress(charge, passive);
        if (stateChanged) SetVerticesDirty();
    }

    private void ApplyFrame()
    {
        if (_settings != null) rectTransform.sizeDelta = _settings.FrameSize;
    }

    private void AnimateProgress(float progress, bool immediate)
    {
        if (_hasProgress && Mathf.Approximately(_progress, progress) && Mathf.Approximately(_targetProgress, progress)) return;
        if (!_hasProgress || immediate)
        {
            _progressTween?.Pause();
            _hasProgress = true;
            _targetProgress = _progress = progress;
            SetVerticesDirty();
            return;
        }
        if (Mathf.Approximately(_targetProgress, progress)) return;
        float duration = progress < _targetProgress ? _settings.DischargeEaseSeconds : _settings.ChargeEaseSeconds;
        _targetProgress = progress;
        if (_progressTween == null || !_progressTween.IsActive())
            _progressTween = DOTween.To(() => _progress, value => { _progress = value; SetVerticesDirty(); }, progress, duration)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetAutoKill(false).SetLink(gameObject);
        else
            _progressTween.ChangeEndValue(progress, duration, true).Restart();
    }

    protected override void OnDisable()
    {
        _progressTween?.Kill();
        _progressTween = null;
        _hasProgress = false;
        _hasCharge = false;
        base.OnDisable();
    }

    private void PositionWithinCell()
    {
        var parent = rectTransform.parent;
        Vector2 size = rectTransform.rect.size;
        if (!Unit.currentCell.TryGetVisualBounds(out var bounds))
        {
            rectTransform.localScale = _authoredScale;
            rectTransform.position = Unit.transform.position;
            return;
        }
        var min = parent.InverseTransformPoint(bounds.min);
        var max = parent.InverseTransformPoint(bounds.max);
        float cellWidth = max.x - min.x;
        float cellHeight = max.y - min.y;
        float width = size.x * Mathf.Abs(_authoredScale.x);
        float height = size.y * Mathf.Abs(_authoredScale.y);
        float factor = Mathf.Min(1f, cellWidth * _settings.CellWidthFraction / Mathf.Max(width, 0.0001f),
            cellHeight * (1f - 2f * _settings.CellBottomInset) / Mathf.Max(height, 0.0001f));
        rectTransform.localScale = _authoredScale * factor;
        float side = cellWidth * _settings.CellSideInset;
        float halfWidth = width * factor * 0.5f;
        float centerX = Mathf.Lerp(min.x + side + halfWidth, max.x - side - halfWidth, _settings.HorizontalAnchor);
        var pivot = rectTransform.pivot;
        rectTransform.localPosition = new Vector3(centerX + (pivot.x - 0.5f) * width * factor,
            min.y + cellHeight * _settings.CellBottomInset + pivot.y * height * factor, 0f);
        // Stay inside the cell while placed; follow the unit's world drag offset while dragged.
        if (_dragHandler != null) rectTransform.position += _dragHandler.DragOffset;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var s = _settings;
        var rect = rectTransform.rect;
        if (s == null || rect.width < 4f || rect.height < 4f) return;
        if (!_bound) UnitStatusBadgeOverlay.ApplyShaderGlobals(s);
        var tier = _bound ? _tier : _previewTier;
        float progress = _bound ? _progress : _previewProgress;
        bool passive = _bound ? _passive : _previewPassive;
        bool full = _bound ? _full : !passive && progress >= s.ReadyThreshold;
        var charge = new Vector4(passive ? 0f : progress, passive ? StatePassive : full ? StateFull : StateCharging, _flashStart, 0f);
        var style = s.GetTier(tier);
        float half = s.TagSize * 0.5f;

        switch (s.Style)
        {
            case UnitStatusBadgeSettings.GaugeStyle.TagBar:
            {
                var center = new Vector2(rect.xMin + OutlineUi + half, rect.center.y);
                // The bar tucks under the tag so both read as one plate.
                float barHalf = s.BarHeight * 0.5f;
                float start = center.x + half * 0.5f;
                float halfLength = Mathf.Max(barHalf, (rect.xMax - OutlineUi - start) * 0.5f);
                Bar(mesh, new Vector2(start + halfLength, center.y), halfLength, barHalf, charge);
                Tag(mesh, center, half, tier, style, TagChargeNone, charge);
                break;
            }
            case UnitStatusBadgeSettings.GaugeStyle.TagBorder:
                Tag(mesh, rect.center, half, tier, style, TagChargeBorder, charge);
                break;
            default:
                Tag(mesh, rect.center, half, tier, style, TagChargeFill, charge);
                break;
        }
    }

    // Rounded square plate with the tier number (1 = Normal ... 4 = Legend) or a star; optionally carries the charge.
    private void Tag(VertexHelper mesh, Vector2 center, float half, Tier tier, UnitStatusBadgeSettings.TierStyle style, float mode, Vector4 charge)
    {
        float symbol = style.Symbol == UnitStatusBadgeSettings.TierSymbol.Star ? SymbolStar
            : style.Symbol == UnitStatusBadgeSettings.TierSymbol.StarOutline ? SymbolStarOutline
            : Mathf.Clamp((int)tier + 1, 1, 4);
        charge.w = style.RimWidth;
        // Per-badge phase so glints and effects do not pulse in step across the field.
        float seed = Mathf.Repeat(GetInstanceID() * 0.6180339f, 1f) * 0.99f;
        Quad(mesh, center, Vector2.one * half * TagPad, Vector2.one * TagPad, KindTag, style.Glint ? 1f + seed : 0f, style.Plate, style.Number, charge,
            new Vector4(OutlineUi / Mathf.Max(half, 0.001f), mode, symbol, mode == TagChargeBorder ? _settings.BorderWidth : _settings.FillStrength),
            (float)style.Effect + seed);
    }

    private void Bar(VertexHelper mesh, Vector2 center, float halfLength, float halfHeight, Vector4 charge)
    {
        var s = _settings;
        float scale = Mathf.Max(halfHeight, 0.001f);
        float aspect = halfLength / scale;
        charge.w = s.BarSegments;
        Quad(mesh, center, new Vector2(halfLength + halfHeight * BarPad, halfHeight * (1f + BarPad)), new Vector2(aspect + BarPad, 1f + BarPad),
            KindBar, aspect, Color.white, Color.white, charge, new Vector4(OutlineUi / scale, 0f, 0f, s.BarSegmentGap * 0.5f / scale), 0f);
    }

    // Same absolute outline thickness on tag and bar.
    private float OutlineUi => _settings.OutlineWidth * _settings.TagSize * 0.5f;

    // One quad; "space" is the shape-local coordinate at the quad edge (tag half-size or bar half-height = 1).
    // Colors ride in COLOR and UV3 only: the canvas transforms normal/tangent as directions.
    private void Quad(VertexHelper mesh, Vector2 center, Vector2 halfSize, Vector2 space, float kind, float extra,
        Color primary, Color secondary, Vector4 uv1, Vector4 uv2, float effect)
    {
        int start = mesh.currentVertCount;
        var second = Linear(secondary);
        for (int i = 0; i < 4; i++)
        {
            float sx = i < 2 ? -1f : 1f;
            float sy = i == 0 || i == 3 ? -1f : 1f;
            var vertex = UIVertex.simpleVert;
            vertex.position = center + new Vector2(sx * halfSize.x, sy * halfSize.y);
            vertex.color = new Color(primary.r, primary.g, primary.b, primary.a * color.a);
            vertex.uv0 = new Vector4(sx * space.x, sy * space.y, kind, extra);
            vertex.uv1 = uv1;
            vertex.uv2 = uv2;
            vertex.uv3 = new Vector4(second.r, second.g, second.b, effect);
            mesh.AddVert(vertex);
        }
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start + 2, start + 3, start);
    }

    // The canvas linearizes vertex colors itself; the extra color channel must match it.
    private static Color Linear(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
}
