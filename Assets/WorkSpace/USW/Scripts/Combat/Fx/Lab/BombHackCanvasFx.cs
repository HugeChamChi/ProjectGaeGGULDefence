using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 도형 아틀라스로 해킹 파열·데이터 이동·회로를 한 UI 메시 안에 그린다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BombHackCanvasFx : MaskableGraphic
{
    private struct Quad
    {
        public Vector2 Center, Size;
        public Rect Uv;
        public Color Color;
        public float Angle;
    }
    private readonly List<Quad> _quads = new(2048);
    private Texture _texture;
    /// <inheritdoc/>
    public override Texture mainTexture => _texture != null ? _texture : Texture2D.whiteTexture;

    /// <summary>공유 도형 텍스처를 설정한다.</summary>
    public void Initialize(Texture texture) { _texture = texture; raycastTarget = false; SetMaterialDirty(); }
    /// <summary>다음 프레임 도형을 비운다.</summary>
    public void BeginFrame() => _quads.Clear();
    /// <summary>재사용 스프라이트 한 장을 추가한다.</summary>
    public void Sprite(Sprite sprite, Vector2 center, Vector2 size, Color tint, float angle = 0f)
    {
        if (sprite == null || tint.a <= 0.001f) return;
        Rect r = sprite.textureRect;
        _quads.Add(new Quad { Center = center, Size = size, Color = tint, Angle = angle,
            Uv = new Rect(r.x / sprite.texture.width, r.y / sprite.texture.height, r.width / sprite.texture.width, r.height / sprite.texture.height) });
    }
    /// <summary>같은 아틀라스의 가늘고 긴 도형으로 선을 추가한다.</summary>
    public void Line(Sprite sprite, Vector2 from, Vector2 to, float width, Color tint)
    {
        Vector2 delta = to - from;
        Sprite(sprite, (from + to) * 0.5f, new Vector2(width, delta.magnitude), tint, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
    }
    /// <summary>추가한 도형을 다시 그린다.</summary>
    public void EndFrame() => SetVerticesDirty();

    /// <inheritdoc/>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var q in _quads)
        {
            int n = vh.currentVertCount;
            Quaternion rotation = Quaternion.Euler(0f, 0f, q.Angle);
            Vector3 center = q.Center;
            vh.AddVert(center + rotation * new Vector3(-q.Size.x, -q.Size.y) * 0.5f, q.Color, new Vector2(q.Uv.xMin, q.Uv.yMin));
            vh.AddVert(center + rotation * new Vector3(-q.Size.x, q.Size.y) * 0.5f, q.Color, new Vector2(q.Uv.xMin, q.Uv.yMax));
            vh.AddVert(center + rotation * new Vector3(q.Size.x, q.Size.y) * 0.5f, q.Color, new Vector2(q.Uv.xMax, q.Uv.yMax));
            vh.AddVert(center + rotation * new Vector3(q.Size.x, -q.Size.y) * 0.5f, q.Color, new Vector2(q.Uv.xMax, q.Uv.yMin));
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
