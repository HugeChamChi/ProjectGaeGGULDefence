using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>기존 도형의 픽셀·피벗을 그대로 묶는 씬 공용 아틀라스. 새 아트를 생성하지 않는다.</summary>
public sealed class StatBuffSpriteAtlas : IDisposable
{
    private readonly Dictionary<Sprite, Sprite> _sprites = new();
    private readonly Texture2D _texture;
    /// <summary>인게임에 사용하는 시안의 도형과 공통 모션 요소를 한 장에 모은다.</summary>
    public StatBuffSpriteAtlas(params StatBuffFxStyle[] styles)
    {
        var originals = new List<Sprite>();
        void Add(Sprite sprite) { if (sprite != null && !originals.Contains(sprite)) originals.Add(sprite); }
        void Shape(StatBuffShape shape)
        {
            var pair = StatBuffShapes.Get(shape); Add(pair.Fill); Add(pair.Outline);
        }
        foreach (var style in styles)
        {
            if (style == null) continue;
            Shape(style.Shape); Shape(style.SecondaryShape); Shape(style.Badge);
        }
        Shape(StatBuffShape.Sparkle);
        Add(StatBuffShapes.Dot); Add(StatBuffShapes.Streak); Add(StatBuffShapes.Column);
        Add(StatBuffShapes.Arc); Add(StatBuffShapes.Ring); Add(StatBuffShapes.Pip);
        var textures = new Texture2D[originals.Count];
        for (int i = 0; i < textures.Length; i++) textures[i] = originals[i].texture;
        _texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        { name = "UnitBuff_SharedAtlas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var packed = _texture.PackTextures(textures, 2, 1024, false);
        for (int i = 0; i < originals.Count; i++)
        {
            var original = originals[i]; var rect = packed[i];
            rect = new Rect(rect.x * _texture.width, rect.y * _texture.height, rect.width * _texture.width, rect.height * _texture.height);
            var pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
            var sprite = Sprite.Create(_texture, rect, pivot, original.pixelsPerUnit * rect.width / original.rect.width);
            sprite.name = "UnitBuffAtlas_" + i; _sprites.Add(original, sprite);
        }
        _texture.Apply(false, true);
    }
    /// <summary>원본 도형에 대응하는 아틀라스 스프라이트. 미등록 그림은 원본을 보존한다.</summary>
    public Sprite Resolve(Sprite original) => original != null && _sprites.TryGetValue(original, out var sprite) ? sprite : original;
    /// <summary>씬이 끝나면 전용 스프라이트와 텍스처를 제거한다.</summary>
    public void Dispose()
    {
        foreach (var sprite in _sprites.Values) UnityEngine.Object.Destroy(sprite);
        _sprites.Clear(); UnityEngine.Object.Destroy(_texture);
    }
}
