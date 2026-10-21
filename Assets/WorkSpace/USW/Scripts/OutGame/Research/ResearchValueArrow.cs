using UnityEngine;
using UnityEngine.UI;

/// <summary>현재 수치와 다음 수치 사이에 표시하는 얇은 꺾쇠.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchValueArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        Vector2[] points =
        {
            new(0.18f, 0.08f), new(0.72f, 0.5f), new(0.18f, 0.92f),
            new(0.35f, 0.92f), new(0.89f, 0.5f), new(0.35f, 0.08f)
        };
        foreach (var point in points)
            mesh.AddVert(new Vector3(rect.xMin + point.x * rect.width, rect.yMin + point.y * rect.height), color, Vector2.zero);
        mesh.AddTriangle(0, 1, 5);
        mesh.AddTriangle(1, 4, 5);
        mesh.AddTriangle(1, 2, 3);
        mesh.AddTriangle(1, 3, 4);
    }
}
