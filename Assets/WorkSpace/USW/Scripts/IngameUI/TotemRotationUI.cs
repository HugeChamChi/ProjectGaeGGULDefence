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

    private void Start() { _camera = Camera.main; HideDirectionUi(); }
    /// <summary>Returns clockwise steps from up, or -1 for the cancellation zone.</summary>
    public static int ResolveDirection(Vector2 delta, float deadZone)
    {
        if (delta.sqrMagnitude <= deadZone * deadZone) return -1;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) return delta.x > 0 ? 1 : 3;
        return delta.y > 0 ? 0 : 2;
    }
    /// <summary>Begins rotation around the placed totem.</summary>
    public void Begin(TotemBase totem)
    {
        Cancel();
        if (totem == null || !totem.IsActive || totem.Data == null || !totem.Data.isRotatable || _settings == null || _gridManager == null) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;
        _totem = totem; _candidate = -1;
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
        int candidate = ResolveDirection(pointer - _center, _settings.RotationDeadZonePixels * _canvasScale);
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
