using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회전 UI 실험실 전용 도형 스프라이트를 런타임에 만든다(원·링·점선 링·둥근 사각형·삼각형·꺾쇠·원형 구멍).
/// 실험실 비교용이라 아트 리소스 없이 모양만 빠르게 바꾸려는 목적이다. 만든 텍스처는 Dispose에서 정리한다.
/// </summary>
public sealed class TotemRotateLabSprites : IDisposable
{
    private const float PixelsPerUnit = 100f;
    private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

    /// <summary>1×1 월드 단위 흰 사각(월드 선 그리기용).</summary>
    public Sprite Pixel { get; }
    /// <summary>채운 원.</summary>
    public Sprite Disc { get; }
    /// <summary>두께 있는 링(Filled Radial360으로 호를 그릴 때도 쓴다).</summary>
    public Sprite Ring { get; }
    /// <summary>두꺼운 링(세그먼트 휠).</summary>
    public Sprite ThickRing { get; }
    /// <summary>얇은 링.</summary>
    public Sprite ThinRing { get; }
    /// <summary>점선 링(취소 영역 표시).</summary>
    public Sprite DashedRing { get; }
    /// <summary>9-slice 둥근 사각형.</summary>
    public Sprite RoundRect { get; }
    /// <summary>9-slice 둥근 사각 테두리(가는 선).</summary>
    public Sprite Frame { get; }
    /// <summary>위를 가리키는 삼각형.</summary>
    public Sprite Triangle { get; }
    /// <summary>위를 가리키는 꺾쇠.</summary>
    public Sprite Chevron { get; }
    /// <summary>가운데가 뚫린 어둠(스포트라이트). 반지름 0.12까지 투명, 0.2부터 불투명.</summary>
    public Sprite Spotlight { get; }
    /// <summary>가장자리만 어두운 비네트.</summary>
    public Sprite Vignette { get; }

    /// <summary>모든 도형을 한 번에 만든다.</summary>
    public TotemRotateLabSprites()
    {
        Pixel = MakePixel();
        Disc = Make("Disc", 128, 128, (x, y) => Fill(Circle(x, y, 64f, 63f)));
        Ring = Make("Ring", 256, 256, (x, y) => Fill(Mathf.Abs(Circle(x, y, 128f, 118f)) - 10f));
        ThickRing = Make("ThickRing", 256, 256, (x, y) => Fill(Mathf.Abs(Circle(x, y, 128f, 107f)) - 20f));
        ThinRing = Make("ThinRing", 256, 256, (x, y) => Fill(Mathf.Abs(Circle(x, y, 128f, 124f)) - 2.2f));
        DashedRing = Make("DashedRing", 128, 128, (x, y) =>
        {
            float ring = Fill(Mathf.Abs(Circle(x, y, 64f, 59f)) - 3f);
            float angle = Mathf.Atan2(y - 64f, x - 64f) / (Mathf.PI * 2f) * 16f;
            float dash = Mathf.Repeat(angle, 1f) < 0.55f ? 1f : 0f;
            return ring * dash;
        });
        RoundRect = Make("RoundRect", 64, 64, (x, y) => Fill(Box(x - 32f, y - 32f, 31f, 31f, 18f)), new Vector4(24, 24, 24, 24));
        Frame = Make("Frame", 64, 64, (x, y) => Fill(Mathf.Abs(Box(x - 32f, y - 32f, 30f, 30f, 10f)) - 1.6f), new Vector4(20, 20, 20, 20));
        Triangle = Make("Triangle", 128, 128, (x, y) => Fill(Tri(x, y)));
        Chevron = Make("Chevron", 128, 128, (x, y) =>
        {
            float d = Mathf.Min(Segment(x, y, 18f, 40f, 64f, 98f), Segment(x, y, 110f, 40f, 64f, 98f));
            return Fill(d - 13f);
        });
        Spotlight = Make("Spotlight", 512, 512, (x, y) =>
        {
            float r = Mathf.Sqrt((x - 256f) * (x - 256f) + (y - 256f) * (y - 256f)) / 256f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.2f, r));
        });
        Vignette = Make("Vignette", 256, 256, (x, y) =>
        {
            float dx = (x - 128f) / 128f, dy = (y - 128f) / 128f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, Mathf.Sqrt(dx * dx + dy * dy)));
        });
    }

    private Sprite Make(string name, int width, int height, Func<float, float, float> alpha, Vector4 border = default)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "TotemRotateLab_" + name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255f));
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        sprite.name = texture.name;
        _owned.Add(texture);
        _owned.Add(sprite);
        return sprite;
    }

    private Sprite MakePixel()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "TotemRotateLab_Pixel", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        sprite.name = texture.name;
        _owned.Add(texture);
        _owned.Add(sprite);
        return sprite;
    }

    // 부호 있는 거리(안쪽 음수) → 1px 안티에일리어싱 알파.
    private static float Fill(float distance) => Mathf.Clamp01(0.5f - distance);
    private static float Circle(float x, float y, float center, float radius) =>
        Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) - radius;
    private static float Box(float x, float y, float halfWidth, float halfHeight, float radius)
    {
        float qx = Mathf.Abs(x) - halfWidth + radius, qy = Mathf.Abs(y) - halfHeight + radius;
        return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }
    private static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
        float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
        float ex = px - dx * h, ey = py - dy * h;
        return Mathf.Sqrt(ex * ex + ey * ey);
    }
    private static float Tri(float x, float y)
    {
        // 꼭짓점 (64,120) (8,12) (120,12) — 세 변까지의 거리 중 최대값(볼록 다각형 근사).
        float d1 = Edge(x, y, 8f, 12f, 120f, 12f);
        float d2 = Edge(x, y, 120f, 12f, 64f, 120f);
        float d3 = Edge(x, y, 64f, 120f, 8f, 12f);
        return Mathf.Max(d1, Mathf.Max(d2, d3));
    }
    private static float Edge(float x, float y, float ax, float ay, float bx, float by)
    {
        float nx = by - ay, ny = -(bx - ax);
        float length = Mathf.Sqrt(nx * nx + ny * ny);
        return ((x - ax) * nx + (y - ay) * ny) / length;
    }

    /// <summary>만든 텍스처와 스프라이트를 정리한다.</summary>
    public void Dispose()
    {
        foreach (var obj in _owned) if (obj != null) UnityEngine.Object.Destroy(obj);
        _owned.Clear();
    }
}
