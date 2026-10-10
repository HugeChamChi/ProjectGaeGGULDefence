using UnityEngine;

/// <summary>Frames authored battle bounds between the HUD bars without moving gameplay objects.</summary>
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(Camera))]
public sealed class BattleViewportFitter : MonoBehaviour
{
    [SerializeField] private RectTransform _topBoundary;
    [SerializeField] private RectTransform _bottomBoundary;
    [SerializeField] private Bounds _battleBounds = new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(7.5f, 10.8f, 1f));
    [SerializeField] private float _padding = 24f;
    private Camera _camera;
    private Vector3 _authoredPosition;
    private Quaternion _authoredRotation;
    private readonly Vector3[] _corners = new Vector3[4];
    private readonly Vector3[] _points = new Vector3[8];
    private Rect _lastViewport;
    private float _lastAspect;

    /// <summary>Screen pixel rectangle reserved for gameplay, excluding safe insets and HUD controls.</summary>
    public Rect PlayArea { get; private set; }

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _authoredPosition = transform.position;
        _authoredRotation = transform.rotation;
    }

    private void OnEnable()
    {
        // Readonly arrays are recreated on an Editor domain reload; Awake is not rerun.
        _lastViewport = Rect.zero;
        int i = 0;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            _points[i++] = Quaternion.Inverse(_authoredRotation) * (_battleBounds.center + Vector3.Scale(_battleBounds.extents, new Vector3(x, y, z)));
    }

    private float Edge(RectTransform rect, bool minimum)
    {
        rect.GetWorldCorners(_corners);
        float edge = minimum ? float.PositiveInfinity : float.NegativeInfinity;
        for (int i = 0; i < _corners.Length; i++)
        {
            float y = RectTransformUtility.WorldToScreenPoint(null, _corners[i]).y;
            edge = minimum ? Mathf.Min(edge, y) : Mathf.Max(edge, y);
        }
        return edge;
    }

    private void LateUpdate()
    {
        if (_topBoundary == null || _bottomBoundary == null || Screen.height <= 0 || _camera.orthographic) return;
        var safe = Screen.safeArea;
        float scale = Mathf.Min(safe.width / 1080f, safe.height / 1920f);
        float pad = _padding * scale;
        PlayArea = Rect.MinMaxRect(safe.xMin + pad, Mathf.Max(safe.yMin, Edge(_bottomBoundary, false)) + pad,
            safe.xMax - pad, Mathf.Min(safe.yMax, Edge(_topBoundary, true)) - pad);
        if (PlayArea.width <= 0 || PlayArea.height <= 0 || (PlayArea == _lastViewport && Mathf.Approximately(_lastAspect, _camera.aspect))) return;
        _lastViewport = PlayArea; _lastAspect = _camera.aspect;
        float tangent = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        var slopeMin = new Vector2((2f * PlayArea.xMin / Screen.width - 1f) * tangent * _camera.aspect, (2f * PlayArea.yMin / Screen.height - 1f) * tangent);
        var slopeMax = new Vector2((2f * PlayArea.xMax / Screen.width - 1f) * tangent * _camera.aspect, (2f * PlayArea.yMax / Screen.height - 1f) * tangent);
        var authored = Quaternion.Inverse(_authoredRotation) * _authoredPosition;
        float near = authored.z;
        float far = near - _battleBounds.size.magnitude * 4f;
        if (!Intervals(near, slopeMin, slopeMax, out _, out _))
        {
            for (int i = 0; i < 32; i++)
            {
                float middle = (near + far) * 0.5f;
                if (Intervals(middle, slopeMin, slopeMax, out _, out _)) far = middle;
                else near = middle;
            }
            near = far;
        }
        if (!Intervals(near, slopeMin, slopeMax, out var min, out var max)) return;
        transform.position = _authoredRotation * new Vector3(Mathf.Clamp(authored.x, min.x, max.x), Mathf.Clamp(authored.y, min.y, max.y), near);
    }

    private bool Intervals(float z, Vector2 slopeMin, Vector2 slopeMax, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        max = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        foreach (var point in _points)
        {
            float depth = point.z - z;
            if (depth <= _camera.nearClipPlane) return false;
            min = Vector2.Max(min, new Vector2(point.x, point.y) - slopeMax * depth);
            max = Vector2.Min(max, new Vector2(point.x, point.y) - slopeMin * depth);
        }
        return min.x <= max.x && min.y <= max.y;
    }

    private void OnDisable()
    {
        transform.position = _authoredPosition;
        _lastViewport = Rect.zero;
    }
}
