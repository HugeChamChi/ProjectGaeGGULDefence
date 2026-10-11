using UnityEngine;
using UnityEngine.UI;

/// <summary>외부 그림 없이 배치 불가 칸과 배치 거부 피드백을 UI 메시로 그린다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BlockedGridCellGraphic : MaskableGraphic
{
    private const int ArcSegments = 16;
    private BlockedGridStyle _style;
    private bool _blocked;
    private bool _visible = true;
    private float _strength = 1f;
    private float _progress = 1f;
    private float _rejection;
    private Color _accepted = new Color(.48f, .86f, .76f, .78f);

    /// <summary>이 실험실 칸이 현재 배치를 거부하는지.</summary>
    public bool IsBlocked => _blocked;

    /// <summary>선택된 시안과 적용 강도를 갱신한다.</summary>
    public void Configure(BlockedGridStyle style, bool blocked, float strength, bool visible = true)
    {
        _style = style; _blocked = blocked; _visible = visible; _strength = Mathf.Clamp(strength, .3f, 1.8f);
        SetVerticesDirty();
    }

    /// <summary>등장 진행과 짧은 배치 거부 강조를 갱신한다.</summary>
    public void SetFeedback(float progress, float rejection)
    {
        if (Mathf.Abs(_progress - progress) < .006f && Mathf.Abs(_rejection - rejection) < .006f) return;
        _progress = progress; _rejection = rejection; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float size = Mathf.Min(r.width, r.height);
        if (size <= 0f) return;
        Rect inset = Inset(r, size * .04f);
        Fill(vh, inset, new Color(.91f, .98f, .97f, .075f));
        Frame(vh, inset, size * .014f, new Color(.78f, .91f, .92f, .30f));
        if (_blocked && _visible && _style != null)
        {
            Fill(vh, inset, Tone(_style.Fill));
            float edge = size * _style.EdgeWidth;
            Rect inside = Inset(inset, size * .045f);
            switch (_style.Kind)
            {
                case BlockedGridStyleKind.HatchLock:
                    Hatch(vh, inside, size);
                    Frame(vh, inset, edge, Tone(_style.Edge));
                    Lock(vh, inset.center, size * .23f);
                    break;
                case BlockedGridStyleKind.CornerClamp:
                    Frame(vh, inset, edge * .45f, Tone(_style.Edge));
                    Corners(vh, inset, size * .23f, edge * 1.8f, Tone(_style.Symbol));
                    Lock(vh, inset.center, size * .28f);
                    break;
                case BlockedGridStyleKind.ChainSeal:
                    Frame(vh, inset, edge * .65f, Tone(_style.Edge));
                    Chain(vh, inset.center, size, 1f);
                    Chain(vh, inset.center, size, -1f);
                    Lock(vh, inset.center, size * .22f);
                    break;
                case BlockedGridStyleKind.CrossMark:
                    Frame(vh, inset, edge, Tone(_style.Edge));
                    Vector2 d = Vector2.one * size * .27f;
                    Stroke(vh, inset.center - d, inset.center + d, size * .047f, Tone(_style.Symbol));
                    d.y = -d.y;
                    Stroke(vh, inset.center - d, inset.center + d, size * .047f, Tone(_style.Symbol));
                    break;
            }
        }
        if (_rejection > .01f)
        {
            Color c = _blocked ? new Color(1f, .46f, .40f, _rejection) : _accepted * new Color(1f, 1f, 1f, _rejection);
            Frame(vh, inset, size * .046f, c);
        }
    }

    private Color Tone(Color c)
    {
        c.a = Mathf.Clamp01(c.a * _strength * _progress);
        return c;
    }

    private void Hatch(VertexHelper vh, Rect r, float size)
    {
        float gap = size * _style.HatchSpacing;
        for (float t = -r.width; t < r.height; t += gap)
        {
            Vector2 a = new Vector2(r.xMin, r.yMin + t);
            Vector2 b = new Vector2(r.xMax, r.yMin + t + r.width);
            if (a.y < r.yMin) { a.x += r.yMin - a.y; a.y = r.yMin; }
            if (b.y > r.yMax) { b.x -= b.y - r.yMax; b.y = r.yMax; }
            if (a.x < b.x && a.y <= r.yMax && b.y >= r.yMin)
                Stroke(vh, a, b, size * .017f, Tone(_style.Pattern));
        }
    }

    private void Lock(VertexHelper vh, Vector2 center, float size)
    {
        Color c = Tone(_style.Symbol);
        Fill(vh, new Rect(center.x - size * .43f, center.y - size * .32f, size * .86f, size * .64f), c);
        Arc(vh, center + Vector2.up * size * .31f, size * .29f, size * .11f, 0f, Mathf.PI, c);
        Color hole = Tone(new Color(_style.Fill.r, _style.Fill.g, _style.Fill.b, .95f));
        Fill(vh, new Rect(center.x - size * .05f, center.y - size * .16f, size * .10f, size * .23f), hole);
    }

    private void Chain(VertexHelper vh, Vector2 center, float size, float direction)
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
                Stroke(vh, previous, next, size * .016f, Tone(_style.Pattern));
                previous = next;
            }
        }
    }

    private static Rect Inset(Rect r, float padding) => new Rect(r.x + padding, r.y + padding, Mathf.Max(0, r.width - padding * 2f), Mathf.Max(0, r.height - padding * 2f));

    private static void Corners(VertexHelper vh, Rect r, float length, float width, Color c)
    {
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
        {
            Vector2 p = new Vector2(x == 0 ? r.xMin : r.xMax, y == 0 ? r.yMin : r.yMax);
            Stroke(vh, p, p + Vector2.right * (x == 0 ? length : -length), width, c);
            Stroke(vh, p, p + Vector2.up * (y == 0 ? length : -length), width, c);
        }
    }

    private static void Arc(VertexHelper vh, Vector2 center, float radius, float width, float from, float to, Color c)
    {
        Vector2 previous = center + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * radius;
        for (int i = 1; i <= ArcSegments; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)ArcSegments);
            Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Stroke(vh, previous, next, width, c); previous = next;
        }
    }

    private static void Frame(VertexHelper vh, Rect r, float width, Color c)
    {
        Fill(vh, new Rect(r.xMin, r.yMin, r.width, width), c);
        Fill(vh, new Rect(r.xMin, r.yMax - width, r.width, width), c);
        Fill(vh, new Rect(r.xMin, r.yMin + width, width, Mathf.Max(0f, r.height - width * 2f)), c);
        Fill(vh, new Rect(r.xMax - width, r.yMin + width, width, Mathf.Max(0f, r.height - width * 2f)), c);
    }

    private static void Fill(VertexHelper vh, Rect r, Color c)
    {
        Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), c);
    }

    private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = b - a;
        if (d.sqrMagnitude < .001f || c.a <= 0f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
        Quad(vh, a - n, a + n, b + n, b - n, c);
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
