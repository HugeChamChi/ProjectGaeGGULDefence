using UnityEngine;

/// <summary>Fits a portrait design frame inside the real safe area without scaling full-screen effects.</summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaContentFrame : MonoBehaviour
{
    [SerializeField] private Vector2 _referenceResolution = new Vector2(1080f, 1920f);
    [SerializeField] private bool _expandHeight = true;
    private RectTransform _rect;
    private RectTransform _parent;
    private Canvas _canvas;
    private Rect _lastSafe;
    private Vector2 _lastScreen;
    private float _lastParentScale;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _parent = transform.parent as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable() => _lastSafe = Rect.zero;
    private void LateUpdate() => Refresh();

    /// <summary>Keeps generated layouts at the reference height while centering them in the safe area.</summary>
    public void UseFixedHeight()
    {
        _expandHeight = false;
        _lastSafe = Rect.zero;
        Refresh();
    }

    /// <summary>Applies the safe frame in Play only; authored editor transforms are never changed.</summary>
    public void Refresh()
    {
        if (!Application.isPlaying || _parent == null || _canvas == null || Screen.width <= 0 || Screen.height <= 0) return;
        var camera = _canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.rootCanvas.worldCamera;
        var origin = RectTransformUtility.WorldToScreenPoint(camera, _parent.TransformPoint(Vector3.zero));
        var unit = RectTransformUtility.WorldToScreenPoint(camera, _parent.TransformPoint(Vector3.right));
        float parentScale = Vector2.Distance(origin, unit);
        var safe = Screen.safeArea;
        var screen = new Vector2(Screen.width, Screen.height);
        if (parentScale <= 0f || safe.width <= 0f || safe.height <= 0f) return;
        if (_lastSafe == safe && _lastScreen == screen && Mathf.Approximately(parentScale, _lastParentScale)) return;
        _lastSafe = safe; _lastScreen = screen; _lastParentScale = parentScale;
        float scale = Mathf.Min(safe.width / _referenceResolution.x, safe.height / _referenceResolution.y);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, safe.center, camera, out var center);
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.localScale = Vector3.one * (scale / parentScale);
        _rect.sizeDelta = new Vector2(_referenceResolution.x, _expandHeight ? safe.height / scale : _referenceResolution.y);
        _rect.anchoredPosition = center - _parent.rect.center;
    }
}
