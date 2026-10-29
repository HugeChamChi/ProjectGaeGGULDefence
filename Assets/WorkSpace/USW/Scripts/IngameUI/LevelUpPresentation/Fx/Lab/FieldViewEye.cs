using UnityEngine;
using UnityEngine.UI;

/// <summary>Small scalable eye outline for the FxLab field-view button.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class FieldViewEye : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        const int segments = 40;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2 / segments;
            float b = (i + 1) * Mathf.PI * 2 / segments;
            var p = new Vector2(Mathf.Cos(a) * rect.width * 0.46f, Mathf.Sin(a) * rect.height * 0.30f) + rect.center;
            var q = new Vector2(Mathf.Cos(b) * rect.width * 0.46f, Mathf.Sin(b) * rect.height * 0.30f) + rect.center;
            var n = (q - p).normalized;
            n = new Vector2(-n.y, n.x) * 1.6f;
            int k = vh.currentVertCount;
            vh.AddVert(p + n, color, Vector2.zero); vh.AddVert(q + n, color, Vector2.zero);
            vh.AddVert(q - n, color, Vector2.zero); vh.AddVert(p - n, color, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2); vh.AddTriangle(k, k + 2, k + 3);
            k = vh.currentVertCount;
            float radius = rect.height * 0.14f;
            vh.AddVert(rect.center, color, Vector2.zero);
            vh.AddVert(rect.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
            vh.AddVert(rect.center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color, Vector2.zero);
            vh.AddTriangle(k, k + 1, k + 2);
        }
    }
}
