using UnityEngine;
using UnityEngine.UI;

/// <summary>단색 배경 가장자리에만 부드러운 음영을 더한다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchVignetteGraphic : MaskableGraphic
{
    private float _strength;
    public void SetStrength(float strength)
    {
        if (Mathf.Approximately(_strength, strength)) return;
        _strength = strength;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int columns = 24, rows = 32;
        var rect = rectTransform.rect;
        for (int y = 0; y <= rows; y++)
        for (int x = 0; x <= columns; x++)
        {
            float u = (float)x / columns, v = (float)y / rows;
            float dx = u * 2f - 1f, dy = v * 2f - 1f;
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1.7f, dx * dx + dy * dy));
            mesh.AddVert(new Vector3(rect.xMin + u * rect.width, rect.yMin + v * rect.height),
                new Color(0f, 0f, 0f, edge * _strength), new Vector2(u, v));
        }
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            int i = y * (columns + 1) + x;
            mesh.AddTriangle(i, i + columns + 1, i + 1);
            mesh.AddTriangle(i + 1, i + columns + 1, i + columns + 2);
        }
    }
}
