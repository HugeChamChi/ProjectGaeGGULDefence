using UnityEngine;
using UnityEngine.UI;

/// <summary>추천 표시용 얇은 둥근 테두리.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchRecommendationOutline : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        float radius = Mathf.Min(18f, Mathf.Min(rect.width, rect.height) * 0.5f);
        const float stroke = 2.5f;
        const int steps = 8;
        for (int corner = 0; corner < 4; corner++)
        {
            float x = corner == 0 || corner == 3 ? rect.xMin + radius : rect.xMax - radius;
            float y = corner < 2 ? rect.yMin + radius : rect.yMax - radius;
            for (int step = 0; step <= steps; step++)
            {
                float angle = (180f + corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(new Vector2(x, y) + direction * radius, color, Vector2.zero);
                mesh.AddVert(new Vector2(x, y) + direction * (radius - stroke), color, Vector2.zero);
            }
        }
        int pairs = 4 * (steps + 1);
        for (int i = 0; i < pairs; i++)
        {
            int a = i * 2, b = (i + 1) % pairs * 2;
            mesh.AddTriangle(a, b, a + 1);
            mesh.AddTriangle(a + 1, b, b + 1);
        }
    }
}
