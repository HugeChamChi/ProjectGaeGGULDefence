using UnityEngine;
using UnityEngine.UI;

/// <summary>Red vignette confined to a narrow, transparent-centered screen border.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BossEntranceGlow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        const int columns = 64;
        const int rows = 96;
        var rect = rectTransform.rect;
        for (int y = 0; y <= rows; y++)
        {
            float v = (float)y / rows;
            for (int x = 0; x <= columns; x++)
            {
                float u = (float)x / columns;
                float distance = Mathf.Min(Mathf.Min(u, 1 - u) * rect.width,
                    Mathf.Min(v, 1 - v) * rect.height);
                float edge = 1 - Mathf.Clamp01(distance / 95f);
                var tint = color;
                tint.a *= edge * edge;
                vh.AddVert(new Vector3(rect.xMin + rect.width * u, rect.yMin + rect.height * v), tint, Vector2.zero);
                if (x == columns || y == rows) continue;
                int index = y * (columns + 1) + x;
                vh.AddTriangle(index, index + columns + 1, index + 1);
                vh.AddTriangle(index + 1, index + columns + 1, index + columns + 2);
            }
        }
    }
}
