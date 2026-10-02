using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 중앙 알림이 쓰는 절차 생성 스프라이트 (아트 에셋 없이 동작). 소유자가 Dispose로 해제한다.
/// 아트가 나오면 CenterToast의 스프라이트 슬롯으로 교체하면 된다.
/// </summary>
public sealed class CenterToastSprites : IDisposable
{
    private const int PillSize = 64;
    private const int RoundedSize = 64;
    private const float RoundedRadius = 16f;
    private const int CircleSize = 64;
    private const int GlowSize = 64;
    private const int BarWidth = 256;
    private const int BarHeight = 64;
    private const float BarFadeX = 0.3f;
    private const float BarFadeY = 0.35f;

    private readonly List<Object> _owned = new List<Object>();

    /// <summary>둥근 알약 (9-slice, 양끝 반원). Image.pixelsPerUnitMultiplier = PillBorder / (높이/2).</summary>
    public Sprite Pill { get; }

    /// <summary>Pill의 9-slice 테두리(px).</summary>
    public float PillBorder => PillSize * 0.5f - 1f;

    /// <summary>모서리가 살짝 둥근 사각형 (9-slice, 반지름 16px). 원하는 반지름 r이면 pixelsPerUnitMultiplier = RoundedBorder / r.</summary>
    public Sprite RoundedRect { get; }

    /// <summary>RoundedRect의 9-slice 테두리(px) = 원본 모서리 반지름.</summary>
    public float RoundedBorder => RoundedRadius;

    /// <summary>꽉 찬 원 (아이콘 바탕).</summary>
    public Sprite Circle { get; }

    /// <summary>가로 양끝·위아래가 부드럽게 사라지는 띠 (A 스택 바탕).</summary>
    public Sprite SoftBar { get; }

    /// <summary>가운데 진하고 바깥으로 사라지는 빛 (D 글자 뒤).</summary>
    public Sprite Glow { get; }

    /// <summary>스프라이트 5종을 만든다.</summary>
    public CenterToastSprites()
    {
        float half = PillBorder;
        Pill = Make(PillSize, PillSize, (dx, dy) => Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * PillSize * 0.5f), new Vector4(half, half, half, half));
        RoundedRect = Make(RoundedSize, RoundedSize, (dx, dy) =>
        {
            // 픽셀 단위 둥근 사각형 거리장 (가장자리 1px 부드럽게)
            float h = RoundedSize * 0.5f;
            float qx = Mathf.Max(Mathf.Abs(dx) * h - (h - RoundedRadius), 0f);
            float qy = Mathf.Max(Mathf.Abs(dy) * h - (h - RoundedRadius), 0f);
            return Mathf.Clamp01(0.5f - (Mathf.Sqrt(qx * qx + qy * qy) - RoundedRadius));
        }, new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius));
        Circle = Make(CircleSize, CircleSize, (dx, dy) => Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * CircleSize * 0.5f), Vector4.zero);
        Glow = Make(GlowSize, GlowSize, (dx, dy) => { float k = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)); return k * k; }, Vector4.zero);
        SoftBar = Make(BarWidth, BarHeight, (dx, dy) =>
            Mathf.SmoothStep(0f, 1f, (1f - Mathf.Abs(dx)) / (BarFadeX * 2f)) * Mathf.SmoothStep(0f, 1f, (1f - Mathf.Abs(dy)) / (BarFadeY * 2f)), Vector4.zero);
    }

    // dx, dy = -1~1 (가운데 0)
    private Sprite Make(int width, int height, Func<float, float, float> alpha, Vector4 border)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "CenterToast_Generated",
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float dx = (x + 0.5f) / width * 2f - 1f;
            float dy = (y + 0.5f) / height * 2f - 1f;
            pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(dx, dy)) * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _owned.Add(sprite);
        _owned.Add(tex);
        return sprite;
    }

    /// <summary>만든 스프라이트·텍스처를 해제한다.</summary>
    public void Dispose()
    {
        foreach (var o in _owned)
            if (o != null) Object.Destroy(o);
        _owned.Clear();
    }
}
