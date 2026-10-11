using UnityEngine;
using UnityEngine.UI;

/// <summary>작은 추천 캡슐도 확대 시 선명하게 그린다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchRecommendationCapsule : MaskableGraphic
{
    public float Radius = 1000f;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        float radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * 0.5f);
        mesh.AddVert(rect.center, color, Vector2.zero);
        const int steps = 12;
        for (int corner = 0; corner < 4; corner++)
        {
            float x = corner == 0 || corner == 3 ? rect.xMin + radius : rect.xMax - radius;
            float y = corner < 2 ? rect.yMin + radius : rect.yMax - radius;
            for (int step = 0; step <= steps; step++)
            {
                float angle = (180f + corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                mesh.AddVert(new Vector2(x, y) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
            }
        }
        int count = 4 * (steps + 1);
        for (int i = 0; i < count; i++) mesh.AddTriangle(0, i + 1, (i + 1) % count + 1);
    }
}
