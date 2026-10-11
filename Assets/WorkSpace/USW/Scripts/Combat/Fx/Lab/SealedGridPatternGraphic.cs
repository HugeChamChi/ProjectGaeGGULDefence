using UnityEngine;
using UnityEngine.UI;

/// <summary>B 색상 위에 사선 외 봉인 패턴과 독립 자물쇠를 그린다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SealedGridPatternGraphic : MaskableGraphic
{
    private const int ArcSegments = 24;
    private const float InsetRatio = .04f;
    private const float EdgeRatio = .025f;
    private const float LockRatio = .23f;
    private static readonly Color EdgeColor = new Color(.70f, .77f, .79f, .75f);
    private static readonly Color LockColor = new Color(.91f, .95f, .96f, 1f);
    private SealedGridShaderVariant _variant;
    private bool _showLock;

    /// <summary>표시할 패턴 데이터와 자물쇠 표시 여부를 갱신한다.</summary>
    public void Configure(SealedGridShaderVariant variant, bool showLock)
    {
        _variant = variant;
        _showLock = showLock;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_variant == null) return;
        Rect rect = GetPixelAdjustedRect();
        float size = Mathf.Min(rect.width, rect.height);
        if (size <= 0f) return;
        Rect inset = Inset(rect, size * InsetRatio);
        Fill(vh, inset, new Color(.91f, .98f, .97f, .075f));
        Frame(vh, inset, size * .014f, new Color(.78f, .91f, .92f, .30f));
        Frame(vh, inset, size * EdgeRatio, EdgeColor);
        Color pattern = _variant.StripeColor;
        pattern.a *= _variant.PatternOpacity;
        float width = size * _variant.PatternLineWidth;
        float span = size * _variant.PatternScale * .5f;
        Vector2 center = inset.center;
        switch (_variant.PatternKind)
        {
            case SealedGridPatternKind.CrossLock:
            case SealedGridPatternKind.ThinCrossLock:
                Cross(vh, center, span, size * .135f, width, pattern);
                break;
            case SealedGridPatternKind.CornerLock:
                Corners(vh, Inset(inset, size * .065f), size * .20f, width, pattern);
                break;
            case SealedGridPatternKind.ChainLock:
                Chain(vh, center, size, 1f, pattern);
                Chain(vh, center, size, -1f, pattern);
                break;
            case SealedGridPatternKind.ProhibitionLock:
                Arc(vh, center, span, width, 0f, Mathf.PI * 2f, pattern);
                Diagonal(vh, center, new Vector2(1f, -1f), span * .70f, size * .13f, width, pattern);
                break;
            case SealedGridPatternKind.LatticeLock:
                Rect inside = Inset(inset, size * .065f);
                for (int i = -2; i <= 2; i++)
                {
                    float offset = i * size * .14f;
                    Stroke(vh, new Vector2(center.x + offset, inside.yMin), new Vector2(center.x + offset, inside.yMax), width, pattern);
                    Stroke(vh, new Vector2(inside.xMin, center.y + offset), new Vector2(inside.xMax, center.y + offset), width, pattern);
                }
                break;
            case SealedGridPatternKind.DoubleFrameLock:
                Frame(vh, Inset(inset, size * .085f), width, pattern);
                break;
            case SealedGridPatternKind.FrameLock:
                Frame(vh, Inset(inset, size * .04f), width, pattern);
                break;
        }
        if (_showLock)
        {
            if (_variant.PatternKind == SealedGridPatternKind.ChainLock || _variant.PatternKind == SealedGridPatternKind.LatticeLock)
            {
                Color backing = _variant.FillColor; backing.a = 1f;
                Fill(vh, new Rect(center - Vector2.one * size * .145f, Vector2.one * size * .29f), backing);
            }
            Lock(vh, center, size * LockRatio);
        }
    }

    private static void Cross(VertexHelper vh, Vector2 center, float extent, float gap, float width, Color color)
    {
        Diagonal(vh, center, new Vector2(1f, 1f), extent, gap, width, color);
        Diagonal(vh, center, new Vector2(1f, -1f), extent, gap, width, color);
    }

    private static void Diagonal(VertexHelper vh, Vector2 center, Vector2 direction, float extent, float gap, float width, Color color)
    {
        Stroke(vh, center - direction * extent, center - direction * gap, width, color);
        Stroke(vh, center + direction * gap, center + direction * extent, width, color);
    }

    private static void Lock(VertexHelper vh, Vector2 center, float size)
    {
        Fill(vh, new Rect(center.x - size * .43f, center.y - size * .32f, size * .86f, size * .64f), LockColor);
        Arc(vh, center + Vector2.up * size * .31f, size * .29f, size * .11f, 0f, Mathf.PI, LockColor);
        Fill(vh, new Rect(center.x - size * .05f, center.y - size * .16f, size * .10f, size * .23f), new Color(0f, 0f, 0f, .95f));
    }

    private static void Chain(VertexHelper vh, Vector2 center, float size, float direction, Color color)
    {
        Vector2 axis = new Vector2(1f, direction).normalized;
        Vector2 across = new Vector2(-axis.y, axis.x);
        for (int link = -3; link <= 3; link++)
        {
            Vector2 origin = center + axis * (link * size * .105f);
            Vector2 previous = origin + axis * size * .065f;
            for (int i = 1; i <= ArcSegments; i++)
            {
                float a = i * 2f * Mathf.PI / ArcSegments;
                Vector2 next = origin + axis * (Mathf.Cos(a) * size * .065f) + across * (Mathf.Sin(a) * size * .035f);
                Stroke(vh, previous, next, size * .016f, color);
                previous = next;
            }
        }
    }

    private static Rect Inset(Rect r, float padding) => new Rect(r.x + padding, r.y + padding, Mathf.Max(0f, r.width - padding * 2f), Mathf.Max(0f, r.height - padding * 2f));

    private static void Corners(VertexHelper vh, Rect r, float length, float width, Color color)
    {
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
        {
            Vector2 p = new Vector2(x == 0 ? r.xMin : r.xMax, y == 0 ? r.yMin : r.yMax);
            Stroke(vh, p, p + Vector2.right * (x == 0 ? length : -length), width, color);
            Stroke(vh, p, p + Vector2.up * (y == 0 ? length : -length), width, color);
        }
    }

    private static void Arc(VertexHelper vh, Vector2 center, float radius, float width, float from, float to, Color color)
    {
        Vector2 previous = center + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * radius;
        for (int i = 1; i <= ArcSegments; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)ArcSegments);
            Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Stroke(vh, previous, next, width, color); previous = next;
        }
    }

    private static void Frame(VertexHelper vh, Rect r, float width, Color color)
    {
        Fill(vh, new Rect(r.xMin, r.yMin, r.width, width), color);
        Fill(vh, new Rect(r.xMin, r.yMax - width, r.width, width), color);
        Fill(vh, new Rect(r.xMin, r.yMin + width, width, Mathf.Max(0f, r.height - width * 2f)), color);
        Fill(vh, new Rect(r.xMax - width, r.yMin + width, width, Mathf.Max(0f, r.height - width * 2f)), color);
    }

    private static void Fill(VertexHelper vh, Rect r, Color color) =>
        Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), color);

    private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < .001f || color.a <= 0f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
        Quad(vh, a - n, a + n, b + n, b - n, color);
    }

    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        if (color.a <= 0f) return;
        int start = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
}
