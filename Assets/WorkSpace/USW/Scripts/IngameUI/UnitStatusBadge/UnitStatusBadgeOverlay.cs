using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One world canvas for every unit's status badge: creates, places and retires them. All badges share one
/// material and texture, so the whole field batches into one draw call while nothing interleaves.
/// </summary>
public sealed class UnitStatusBadgeOverlay : MonoBehaviour
{
    /// <summary>Vertex channels the badge shader reads beyond position/color/uv0.</summary>
    public const AdditionalCanvasShaderChannels RequiredChannels = AdditionalCanvasShaderChannels.TexCoord1
        | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
    private const string ResourcePath = "UnitStatusBadge";
    // Badges always draw above units and drones. Within the "Unit" layer drone sprites still draw on top
    // (measured 2026-09-24), so the canvas sits one layer up, as the previous status overlay did.
    private const string SortingLayerAboveUnits = "FX";
    private const int SortingOrderAboveUnits = 29000;
    private static readonly int TimeId = Shader.PropertyToID("_UnitStatusTime");
    private static readonly int TimingId = Shader.PropertyToID("_UnitStatusTiming");
    private static readonly int OutlineId = Shader.PropertyToID("_UnitStatusOutline");
    private static readonly int FillId = Shader.PropertyToID("_UnitStatusFill");
    private static readonly int FullId = Shader.PropertyToID("_UnitStatusFull");
    private static readonly int TrackId = Shader.PropertyToID("_UnitStatusTrack");
    private static readonly int PassiveTrackId = Shader.PropertyToID("_UnitStatusPassiveTrack");
    private static readonly int PassiveStripeId = Shader.PropertyToID("_UnitStatusPassiveStripe");
    private static readonly int GlintId = Shader.PropertyToID("_UnitStatusGlint");
    private static readonly int EffectId = Shader.PropertyToID("_UnitStatusEffect");

    private readonly List<UnitStatusBadge> _views = new List<UnitStatusBadge>();
    private UnitStatusBadge _prefab;
    private UnitStatusBadgeSettings _settings;

    /// <summary>Badges currently owned.</summary>
    public IReadOnlyList<UnitStatusBadge> Views => _views;

    /// <summary>Look and timing for every badge. Defaults to the prefab's settings.</summary>
    public UnitStatusBadgeSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            foreach (var view in _views)
                if (view != null) view.Settings = value;
            ApplyShaderGlobals(_settings);
        }
    }

    /// <summary>Creates the grid-lifetime canvas. Without a prefab, uses Resources/UnitStatusBadge.</summary>
    public static UnitStatusBadgeOverlay Create(Transform owner, UnitStatusBadge prefab = null)
    {
        var root = new GameObject("Unit Status Badge Overlay", typeof(RectTransform), typeof(Canvas));
        root.layer = owner.gameObject.layer;
        root.transform.SetParent(owner, true);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = SortingLayerAboveUnits;
        canvas.sortingOrder = SortingOrderAboveUnits;
        canvas.additionalShaderChannels |= RequiredChannels;
        ((RectTransform)root.transform).sizeDelta = new Vector2(100f, 100f);
        var overlay = root.AddComponent<UnitStatusBadgeOverlay>();
        overlay._prefab = prefab != null ? prefab : Resources.Load<UnitStatusBadge>(ResourcePath);
        if (overlay._prefab == null) Debug.LogError("UnitStatusBadge: Resources/UnitStatusBadge prefab is missing.", owner);
        else overlay._settings = overlay._prefab.Settings;
        ApplyShaderGlobals(overlay._settings);
        return overlay;
    }

    /// <summary>Connects one badge to one unit. Calling again for the same unit does nothing.</summary>
    public void Register(UnitBase unit)
    {
        if (unit == null || _prefab == null) return;
        foreach (var view in _views)
            if (view != null && view.Unit == unit) return;
        var instance = Instantiate(_prefab, transform, false);
        instance.name = "Status - " + unit.name;
        if (_settings != null && instance.Settings != _settings) instance.Settings = _settings;
        instance.Bind(unit);
        _views.Add(instance);
    }

    private void LateUpdate()
    {
        ApplyShaderGlobals(_settings);
        for (int i = _views.Count - 1; i >= 0; i--)
        {
            var view = _views[i];
            if (view == null || view.Unit == null)
            {
                if (view != null) Destroy(view.gameObject);
                _views.RemoveAt(i);
                continue;
            }
            view.Refresh();
        }
    }

    /// <summary>
    /// Shared clock, timings and settings-wide colors: blinks and the full pulse animate in the shader
    /// without rebuilding meshes. Called every frame by the overlay; prefab previews call it too.
    /// </summary>
    public static void ApplyShaderGlobals(UnitStatusBadgeSettings settings)
    {
        Shader.SetGlobalFloat(TimeId, Time.unscaledTime);
        if (settings == null) return;
        Shader.SetGlobalVector(TimingId, new Vector4(settings.ReadyFlashSeconds, settings.FullPulseSeconds, settings.FullGlow, settings.GlintPeriodSeconds));
        Shader.SetGlobalColor(OutlineId, settings.OutlineColor);
        Shader.SetGlobalColor(FillId, settings.GaugeFill);
        Shader.SetGlobalColor(FullId, settings.GaugeFull);
        Shader.SetGlobalColor(TrackId, settings.GaugeTrack);
        Shader.SetGlobalColor(PassiveTrackId, settings.PassiveTrack);
        Shader.SetGlobalColor(PassiveStripeId, settings.PassiveStripe);
        Shader.SetGlobalColor(GlintId, settings.GlintColor);
        Shader.SetGlobalVector(EffectId, new Vector4(settings.EffectPeriodSeconds, settings.EffectSweepSeconds, 0f, 0f));
    }
}
