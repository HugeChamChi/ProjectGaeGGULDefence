using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 볼록 다각형 UGUI 그래픽 (토템 보상 사선 띠, 뒤로가기 삼각형 등).
/// 꼭짓점은 RectTransform 안의 정규화 좌표(0~1, 좌하단 원점)로 주고, 채움 색은 Graphic.color를 쓴다.
/// 선택한 변에만 검은 테두리 띠를 그린다. 터치 판정도 같은 다각형이라 사선 경계에서 옆 띠가 눌리지 않는다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIPolygonGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    [SerializeField] private List<Vector2> _points = new List<Vector2>();
    [SerializeField] private Color _edgeColor = Color.black;
    [SerializeField, Min(0f)] private float _edgeWidth = 12f;
    [Tooltip("테두리를 그릴 변의 시작 꼭짓점 번호 (i → i+1). 비우면 테두리 없음.")]
    [SerializeField] private List<int> _edges = new List<int>();

    /// <summary>꼭짓점(정규화 좌표, 시계/반시계 순서)과 테두리 변을 설정한다.</summary>
    public void SetShape(IReadOnlyList<Vector2> points, IReadOnlyList<int> edges, Color edgeColor, float edgeWidth)
    {
        _points.Clear();
        _points.AddRange(points);
        _edges.Clear();
        if (edges != null) _edges.AddRange(edges);
        _edgeColor = edgeColor;
        _edgeWidth = edgeWidth;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int n = _points.Count;
        if (n < 3) return;

        var rect = rectTransform.rect;
        var fill = (Color32)color;
        for (int i = 0; i < n; i++) vh.AddVert(ToLocal(rect, _points[i]), fill, Vector4.zero);
        for (int i = 1; i < n - 1; i++) vh.AddTriangle(0, i, i + 1);

        if (_edgeWidth <= 0f) return;
        var edge = (Color32)_edgeColor;
        float half = _edgeWidth * 0.5f;
        foreach (int e in _edges)
        {
            if (e < 0 || e >= n) continue;
            var a = ToLocal(rect, _points[e]);
            var b = ToLocal(rect, _points[(e + 1) % n]);
            var dir = (b - a).normalized;
            var normal = new Vector2(-dir.y, dir.x) * half;
            // 끝을 반 폭만큼 늘려 이웃 띠의 테두리와 이음새 없이 겹친다.
            a -= dir * half; b += dir * half;
            int start = vh.currentVertCount;
            vh.AddVert(a - normal, edge, Vector4.zero);
            vh.AddVert(a + normal, edge, Vector4.zero);
            vh.AddVert(b + normal, edge, Vector4.zero);
            vh.AddVert(b - normal, edge, Vector4.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    /// <inheritdoc />
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (_points.Count < 3) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)) return false;
        var rect = rectTransform.rect;
        bool inside = false;
        for (int i = 0, j = _points.Count - 1; i < _points.Count; j = i++)
        {
            var pi = ToLocal(rect, _points[i]);
            var pj = ToLocal(rect, _points[j]);
            if ((pi.y > local.y) != (pj.y > local.y) &&
                local.x < (pj.x - pi.x) * (local.y - pi.y) / (pj.y - pi.y) + pi.x)
                inside = !inside;
        }
        return inside;
    }

    private static Vector2 ToLocal(Rect rect, Vector2 normalized) =>
        new Vector2(rect.xMin + rect.width * normalized.x, rect.yMin + rect.height * normalized.y);
}
