using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchGradientGraphic : MaskableGraphic
{
    public enum Shape { TopFade, BottomFade, RightShade }
    private Shape _shape;

    public void Configure(Shape shape, Color tint)
    {
        _shape = shape;
        color = tint;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int steps = 24;
        var rect = rectTransform.rect;
        for (int y = 0; y <= steps; y++)
        for (int x = 0; x <= steps; x++)
        {
            float u = (float)x / steps, v = (float)y / steps;
            float alpha;
            if (_shape == Shape.RightShade)
                alpha = u;
            else
                alpha = Mathf.SmoothStep(0f, 1f, _shape == Shape.TopFade ? v : 1f - v);
            var tint = color;
            tint.a *= alpha;
            mesh.AddVert(new Vector3(rect.xMin + u * rect.width, rect.yMin + v * rect.height), tint, new Vector2(u, v));
        }
        for (int y = 0; y < steps; y++)
        for (int x = 0; x < steps; x++)
        {
            int i = y * (steps + 1) + x;
            mesh.AddTriangle(i, i + steps + 1, i + 1);
            mesh.AddTriangle(i + 1, i + steps + 1, i + steps + 2);
        }
    }
}
