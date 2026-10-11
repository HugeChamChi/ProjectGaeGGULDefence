using UnityEngine;
using UnityEngine.UI;

/// <summary>캐주얼 추천 표시의 초록 상승 화살표.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchRecommendationArrow : MaskableGraphic
{
    private static readonly Vector2[] Shape =
    {
        new(-0.48f, 0.06f), new(0f, 0.5f), new(0.48f, 0.06f),
        new(0.28f, 0.06f), new(0.28f, -0.45f), new(-0.28f, -0.45f), new(-0.28f, 0.06f)
    };

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Draw(mesh, 1f, new Color32(25, 70, 62, 255), new Color32(25, 70, 62, 255));
        Draw(mesh, 0.94f, new Color32(36, 224, 102, 255), new Color32(38, 237, 142, 255));
        Draw(mesh, 0.82f, new Color32(249, 255, 30, 255), new Color32(112, 255, 39, 255));
    }

    private void Draw(VertexHelper mesh, float scale, Color bottom, Color top)
    {
        var rect = rectTransform.rect;
        int first = mesh.currentVertCount;
        mesh.AddVert(rect.center, Color.Lerp(bottom, top, 0.5f), Vector2.zero);
        foreach (var p in Shape)
            mesh.AddVert(rect.center + new Vector2(p.x * rect.width, p.y * rect.height) * scale,
                Color.Lerp(bottom, top, p.y + 0.5f), Vector2.zero);
        for (int i = 0; i < Shape.Length; i++)
            mesh.AddTriangle(first, first + i + 1, first + (i + 1) % Shape.Length + 1);
    }
}
