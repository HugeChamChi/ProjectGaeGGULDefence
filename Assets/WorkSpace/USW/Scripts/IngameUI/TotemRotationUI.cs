using UnityEngine;
using VContainer;

/// <summary>Four-direction rotation shown only through striped range cells, with a faded central cancellation zone.</summary>
public sealed class TotemRotationUI : MonoBehaviour
{
    [Inject] private TotemInteractionSettings _settings;
    [Inject] private GridManager _gridManager;
    [SerializeField] private RectTransform _root;
    private TotemBase _totem;
    private Camera _camera;
    private Vector2 _center;
    private float _canvasScale = 1f;
    private int _candidate = -1;
    private int _baseFacing;

    private void Start() { _camera = Camera.main; HideDirectionUi(); }
    /// <summary>Returns clockwise steps from up, or -1 for the cancellation zone.</summary>
    public static int ResolveDirection(Vector2 delta, float deadZone)
    {
        if (delta.sqrMagnitude <= deadZone * deadZone) return -1;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) return delta.x > 0 ? 1 : 3;
        return delta.y > 0 ? 0 : 2;
    }

    /// <summary>
    /// Clockwise steps from up that the unrotated effect range points to (0 up, 1 right, 2 down, 3 left).
    /// Symmetric or empty ranges count as facing up, matching the original direction mapping.
    /// </summary>
    public static int BaseFacing(TotemData data)
    {
        var sum = Vector2Int.zero;
        if (data != null) foreach (var offset in data.GetEffectPreviewOffsets()) sum += offset;
        if (sum == Vector2Int.zero) return 0;
        if (Mathf.Abs(sum.x) > Mathf.Abs(sum.y)) return sum.x > 0 ? 1 : 3;
        return sum.y > 0 ? 0 : 2;
    }

    /// <summary>Rotation step that points a range facing <paramref name="baseFacing"/> toward the dragged screen direction.</summary>
    public static int StepToward(int direction, int baseFacing) => direction < 0 ? -1 : ((direction - baseFacing) % 4 + 4) % 4;
    /// <summary>Begins rotation around the placed totem.</summary>
    public void Begin(TotemBase totem)
    {
        Cancel();
        if (totem == null || !totem.IsActive || totem.Data == null || !totem.Data.isRotatable || _settings == null || _gridManager == null) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;
        _totem = totem; _candidate = -1;
        // Level totems author their front as (0,-1); dragging toward a unit must still aim the range at it.
        _baseFacing = BaseFacing(totem.Data);
        _center = _camera.WorldToScreenPoint(totem.transform.position);
        var canvas = _root != null ? _root.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
        _canvasScale = canvas != null ? canvas.scaleFactor : 1f;
        HideDirectionUi();
        _gridManager.ShowTotemRangePreview(totem, _settings.RotationCancelPreviewOpacity);
    }
    /// <summary>Updates only the range preview, never the applied buffs.</summary>
    public void Drag(Vector2 worldPosition)
    {
        if (_totem == null || !_totem.IsActive) { Cancel(); return; }
        Vector2 pointer = _camera.WorldToScreenPoint(worldPosition);
        int candidate = StepToward(ResolveDirection(pointer - _center, _settings.RotationDeadZonePixels * _canvasScale), _baseFacing);
        if (candidate == _candidate) return;
        _candidate = candidate;
        if (candidate < 0) _gridManager.ShowTotemRangePreview(_totem, _settings.RotationCancelPreviewOpacity);
        else _totem.PreviewRotation(candidate);
    }
    /// <summary>Releasing in the center cancels; otherwise commits the preview direction.</summary>
    public void End(Vector2 position)
    {
        Drag(position);
        if (_totem != null && _candidate >= 0) _totem.SetRotationStep(_candidate);
        Cancel();
    }
    /// <summary>Dismisses rotation and range overlays after release or cancellation.</summary>
    public void Cancel()
    {
        if (_totem != null) _gridManager?.ClearTotemRangePreview();
        _totem = null; _candidate = -1;
        HideDirectionUi();
    }
    private void HideDirectionUi()
    {
        if (_root != null) _root.gameObject.SetActive(false);
    }
    private void OnDisable() => Cancel();
}
