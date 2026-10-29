using UnityEngine;
using UnityEngine.UI;

/// <summary>Small outlined vector icon (hourglass or heart) for HUD labels; no sprite needed.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HudGlyph : MaskableGraphic
{
    /// <summary>Drawn shape.</summary>
    public enum Shape { Hourglass, Heart }

    [SerializeField] private Shape _shape;
    [SerializeField] private Color _outlineColor = Color.black;
    [SerializeField, Min(0f)] private float _outlineWidth = 3f;

    private const int HeartSegments = 48;

    /// <summary>Shape to draw.</summary>
    public Shape GlyphShape { get => _shape; set { _shape = value; SetVerticesDirty(); } }

    /// <summary>Outline drawn behind the fill.</summary>
    public Color OutlineColor { get => _outlineColor; set { _outlineColor = value; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        if (_shape == Shape.Heart)
        {
            var heart = HeartPoints(rect);
            var center = rect.center + new Vector2(0f, rect.height * 0.08f);
            Fan(vh, heart, center, _outlineWidth, _outlineColor);
            Fan(vh, heart, center, 0f, color);
            return;
        }
        float w = rect.width * 0.5f - _outlineWidth, h = rect.height * 0.5f - _outlineWidth, cap = h * 0.2f;
        var glass = HourglassGlass(rect.center, w * 0.8f, h - cap, w * 0.14f);
        var top = new Rect(rect.center.x - w, rect.center.y + h - cap, w * 2f, cap);
        var bottom = new Rect(rect.center.x - w, rect.center.y - h, w * 2f, cap);
        Fan(vh, glass, rect.center, _outlineWidth, _outlineColor);
        Quad(vh, top, _outlineWidth, _outlineColor);
        Quad(vh, bottom, _outlineWidth, _outlineColor);
        Fan(vh, glass, rect.center, 0f, color);
        Quad(vh, top, 0f, color);
        Quad(vh, bottom, 0f, color);
    }

    private static void Quad(VertexHelper vh, Rect r, float grow, Color32 tint)
    {
        int k = vh.currentVertCount;
        vh.AddVert(new Vector2(r.xMin - grow, r.yMin - grow), tint, Vector2.zero);
        vh.AddVert(new Vector2(r.xMin - grow, r.yMax + grow), tint, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax + grow, r.yMax + grow), tint, Vector2.zero);
        vh.AddVert(new Vector2(r.xMax + grow, r.yMin - grow), tint, Vector2.zero);
        vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
    }

    // Heart and glass are star-shaped around `center`, so a fan triangulates them and a
    // radial push gives a near-even outline.
    private static void Fan(VertexHelper vh, Vector2[] points, Vector2 center, float grow, Color32 tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        foreach (var p in points)
        {
            var dir = p - center;
            vh.AddVert(dir.sqrMagnitude > 0f ? p + dir.normalized * grow : p, tint, Vector2.zero);
        }
        for (int i = 0; i < points.Length; i++)
            vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
    }

    // Bow-tie glass between the two caps; star-shaped around the neck centre.
    private static Vector2[] HourglassGlass(Vector2 c, float halfWidth, float halfHeight, float neck) => new[]
    {
        c + new Vector2(neck, 0f), c + new Vector2(halfWidth, halfHeight), c + new Vector2(-halfWidth, halfHeight),
        c + new Vector2(-neck, 0f), c + new Vector2(-halfWidth, -halfHeight), c + new Vector2(halfWidth, -halfHeight),
    };

    private Vector2[] HeartPoints(Rect r)
    {
        // Classic parametric heart; x in [-16,16], y in [-17,~12].
        var points = new Vector2[HeartSegments];
        float sx = (r.width * 0.5f - _outlineWidth) / 16f, sy = (r.height - _outlineWidth * 2f) / 29f;
        var origin = new Vector2(r.center.x, r.yMin + _outlineWidth + 17f * sy);
        for (int i = 0; i < HeartSegments; i++)
        {
            float t = i * Mathf.PI * 2f / HeartSegments;
            float s = Mathf.Sin(t);
            float x = 16f * s * s * s;
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            points[i] = origin + new Vector2(x * sx, y * sy);
        }
        return points;
    }
}
