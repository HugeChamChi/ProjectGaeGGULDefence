using UnityEngine;
using UnityEngine.UI;

/// <summary>로비와 비교 실험실이 공유하는, 선택색이 곡선 경계로 어두운 면을 밀어내는 UI 그래픽.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LobbyTabFlowGraphic : MaskableGraphic
{
    private const int Strips = 24;
    private const float CurveStrength = 0.08f;
    private float _reveal;
    private float _direction = 1f;
    private bool _useSelectionWindow;
    private float _windowLeft;
    private float _windowRight;
    private static readonly Color BrightBottom = new Color(0.43f, 0.27f, 0.77f);
    private static readonly Color BrightTop = new Color(0.58f, 0.39f, 0.94f);
    private static readonly Color DarkBottom = new Color(0.14f, 0.12f, 0.20f);
    private static readonly Color DarkTop = new Color(0.25f, 0.21f, 0.35f);

    /// <summary>선택색 노출 비율. 0은 어두운 면, 1은 선택색이다.</summary>
    public float Reveal
    {
        get => _reveal;
        set { _reveal = Mathf.Clamp01(value); SetVerticesDirty(); }
    }

    /// <summary>밝은 면이 들어오는 방향. 양수는 왼쪽에서, 음수는 오른쪽에서 들어온다.</summary>
    public float Direction
    {
        get => _direction;
        set { _direction = value >= 0f ? 1f : -1f; SetVerticesDirty(); }
    }

    /// <summary>공통 선택 배경의 현재 구간을 이 탭 안에 잘라서 표시한다.</summary>
    public void SetSelectionWindow(float left, float right, float direction)
    {
        _useSelectionWindow = true;
        _windowLeft = left;
        _windowRight = right;
        _direction = direction >= 0f ? 1f : -1f;
        _reveal = Mathf.Clamp01(Mathf.Min(1f, right) - Mathf.Max(0f, left));
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        for (int row = 0; row < Strips; row++)
        {
            float y0 = row / (float)Strips;
            float y1 = (row + 1f) / Strips;
            float bottom = Mathf.Lerp(rect.yMin, rect.yMax, y0);
            float top = Mathf.Lerp(rect.yMin, rect.yMax, y1);
            if (_useSelectionWindow)
            {
                Quad(vh, rect.xMin, rect.xMax, rect.xMin, rect.xMax, bottom, top,
                    Color.Lerp(DarkBottom, DarkTop, y0), Color.Lerp(DarkBottom, DarkTop, y1));
                float bend0 = WindowBend(y0);
                float bend1 = WindowBend(y1);
                Quad(vh, WindowEdge(rect, _windowLeft, bend0), WindowEdge(rect, _windowRight, bend0),
                    WindowEdge(rect, _windowLeft, bend1), WindowEdge(rect, _windowRight, bend1), bottom, top,
                    Color.Lerp(BrightBottom, BrightTop, y0), Color.Lerp(BrightBottom, BrightTop, y1));
                continue;
            }
            Quad(vh, rect.xMin, rect.xMax, rect.xMin, rect.xMax, bottom, top,
                Color.Lerp(BrightBottom, BrightTop, y0), Color.Lerp(BrightBottom, BrightTop, y1));
            float edge0 = Edge(rect, y0);
            float edge1 = Edge(rect, y1);
            if (_direction > 0f)
                Quad(vh, edge0, rect.xMax, edge1, rect.xMax, bottom, top,
                    Color.Lerp(DarkBottom, DarkTop, y0), Color.Lerp(DarkBottom, DarkTop, y1));
            else
                Quad(vh, rect.xMin, edge0, rect.xMin, edge1, bottom, top,
                    Color.Lerp(DarkBottom, DarkTop, y0), Color.Lerp(DarkBottom, DarkTop, y1));
        }
    }

    private float WindowBend(float y) => Mathf.Sin(y * Mathf.PI) * Mathf.Sin(_reveal * Mathf.PI) * CurveStrength * _direction;

    private static float WindowEdge(Rect rect, float edge, float bend) => Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Clamp01(edge + bend));

    private float Edge(Rect rect, float y)
    {
        float bulge = Mathf.Sin(y * Mathf.PI) * Mathf.Sin(_reveal * Mathf.PI) * CurveStrength;
        float travel = Mathf.Clamp01(_reveal + bulge);
        return Mathf.Lerp(rect.xMin, rect.xMax, _direction > 0f ? travel : 1f - travel);
    }

    private static void Quad(VertexHelper vh, float leftBottom, float rightBottom, float leftTop,
        float rightTop, float bottom, float top, Color bottomColor, Color topColor)
    {
        int first = vh.currentVertCount;
        vh.AddVert(new Vector3(leftBottom, bottom), bottomColor, Vector2.zero);
        vh.AddVert(new Vector3(leftTop, top), topColor, Vector2.up);
        vh.AddVert(new Vector3(rightTop, top), topColor, Vector2.one);
        vh.AddVert(new Vector3(rightBottom, bottom), bottomColor, Vector2.right);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }
}
