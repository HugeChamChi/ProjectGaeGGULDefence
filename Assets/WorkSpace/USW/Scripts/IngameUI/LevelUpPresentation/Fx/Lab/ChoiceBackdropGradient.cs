using UnityEngine;
using UnityEngine.UI;

/// <summary>선택지 실험실의 아래에서 위로 사라지는 은은한 배경색.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ChoiceBackdropGradient : MaskableGraphic
{
    [SerializeField, Range(0f, 1f)] private float _strength = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float _height = 0.85f;

    /// <summary>아래쪽에 색을 남기고 위로 갈수록 부드럽게 투명해지는 메시를 만든다.</summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        const int bands = 24;
        for (int i = 0; i <= bands; i++)
        {
            float t = (float)i / bands;
            var tint = color;
            tint.a *= _strength * Mathf.Pow(1f - Mathf.Clamp01(t / _height), 1.5f);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, t);
            vh.AddVert(new Vector3(rect.xMin, y), tint, new Vector2(0f, t));
            vh.AddVert(new Vector3(rect.xMax, y), tint, new Vector2(1f, t));
            if (i == 0) continue;
            int k = i * 2;
            vh.AddTriangle(k - 2, k, k - 1);
            vh.AddTriangle(k - 1, k, k + 1);
        }
    }
}
