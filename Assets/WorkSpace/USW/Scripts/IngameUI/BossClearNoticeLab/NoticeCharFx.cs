using TMPro;
using UnityEngine;

/// <summary>
/// TMP 글자 하나하나를 움직이는 도우미 — 글자별 오프셋·크기·투명도.
/// 글자·색을 바꾼 뒤 매 프레임 마지막에 호출한다 (메시를 다시 만든 뒤 정점을 민다).
/// </summary>
public static class NoticeCharFx
{
    /// <summary>글자별 변형 계산기 (index, count) → 결과.</summary>
    public delegate void CharTransform(int index, int count, out Vector2 offset, out float scale, out float alpha);

    /// <summary>text의 보이는 글자마다 transform을 적용한다.</summary>
    public static void Apply(TMP_Text text, CharTransform transform)
    {
        text.ForceMeshUpdate();
        var info = text.textInfo;
        int count = info.characterCount;
        for (int i = 0; i < count; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            transform(i, count, out var offset, out float scale, out float alpha);
            var mesh = info.meshInfo[c.materialReferenceIndex];
            var verts = mesh.vertices;
            var cols = mesh.colors32;
            int vi = c.vertexIndex;
            Vector3 center = (verts[vi] + verts[vi + 2]) * 0.5f;
            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            for (int v = 0; v < 4; v++)
            {
                verts[vi + v] = center + (verts[vi + v] - center) * scale + (Vector3)offset;
                var col = cols[vi + v];
                col.a = (byte)(col.a * a / 255);
                cols[vi + v] = col;
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }
}
