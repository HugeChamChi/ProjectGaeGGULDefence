using UnityEngine;

/// <summary>
/// Extends a background RectTransform that lives under a <see cref="SafeAreaApplier"/> root out to the full
/// screen edges, like the battle scene whose world background fills the notch/status bar area while its HUD
/// stays inside the safe area. Authored offsets are kept; only the screen-edge excess is added.
/// Attach to backgrounds only, never to containers that hold buttons or text. On an object with a
/// RectMask2D it only widens the clip padding so bled children are not cut off; its own rect is untouched.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaBleed : MonoBehaviour
{
    private RectTransform _rect;
    private RectTransform _parent;
    private Canvas _canvas;
    private Vector2 _authoredMin;
    private Vector2 _authoredMax;
    private bool _captured;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreen;
    private Rect _lastParentRect;
    private UnityEngine.UI.RectMask2D _mask;
    private Vector4 _authoredPadding;

    private void Awake() => Capture();

    private void OnEnable()
    {
        Capture();
        Apply();
    }

    private void LateUpdate()
    {
        if (_parent == null) return;
        if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y ||
            _parent.rect != _lastParentRect)
            Apply();
    }

    private void OnDisable()
    {
        if (_captured && _mask != null) _mask.padding = _authoredPadding;
        else if (_captured && _rect != null)
        {
            _rect.offsetMin = _authoredMin;
            _rect.offsetMax = _authoredMax;
        }
    }

    private void Capture()
    {
        if (_captured) return;
        _rect = (RectTransform)transform;
        _parent = _rect.parent as RectTransform;
        _mask = GetComponent<UnityEngine.UI.RectMask2D>();
        if (_mask != null) _authoredPadding = _mask.padding;
        _authoredMin = _rect.offsetMin;
        _authoredMax = _rect.offsetMax;
        _captured = true;
    }

    private void Apply()
    {
        if (_rect == null || _parent == null || Screen.width <= 0 || Screen.height <= 0) return;
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (_canvas == null) return;
        _lastSafeArea = Screen.safeArea;
        _lastScreen = new Vector2Int(Screen.width, Screen.height);
        _lastParentRect = _parent.rect;

        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, Vector2.zero, camera, out var screenMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(Screen.width, Screen.height), camera, out var screenMax);
        Rect parentRect = _parent.rect;
        var grow = new Vector4(
            Mathf.Min(0f, screenMin.x - parentRect.xMin), Mathf.Min(0f, screenMin.y - parentRect.yMin),
            Mathf.Max(0f, screenMax.x - parentRect.xMax), Mathf.Max(0f, screenMax.y - parentRect.yMax));
        if (_mask != null)
        {
            // On a clipping container, widen only the clip area; children keep the safe-area layout.
            Rect own = _rect.rect;
            Vector2 ownMin = (Vector2)_parent.InverseTransformPoint(_rect.TransformPoint(own.min));
            Vector2 ownMax = (Vector2)_parent.InverseTransformPoint(_rect.TransformPoint(own.max));
            _mask.padding = _authoredPadding + new Vector4(Mathf.Min(0f, screenMin.x - ownMin.x), Mathf.Min(0f, screenMin.y - ownMin.y),
                Mathf.Min(0f, ownMax.x - screenMax.x), Mathf.Min(0f, ownMax.y - screenMax.y));
            return;
        }
        // Grow only edges the background already reaches (stretched to that side of its parent).
        _rect.offsetMin = _authoredMin + new Vector2(_rect.anchorMin.x <= 0f ? grow.x : 0f, _rect.anchorMin.y <= 0f ? grow.y : 0f);
        _rect.offsetMax = _authoredMax + new Vector2(_rect.anchorMax.x >= 1f ? grow.z : 0f, _rect.anchorMax.y >= 1f ? grow.w : 0f);
    }
}
