using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 실험실과 디시그망 연결 연출이 쓰는 절차 생성 스프라이트 (부드러운 점·링·글로우·알약).
/// 4종을 텍스처 한 장에 묶어 UI 이미지가 섞여 있어도 한 번에 그려지게 한다.
/// 아트 에셋 없이 시안을 돌리기 위한 것 — 소유자가 Dispose로 텍스처를 해제한다.
/// </summary>
public sealed class TimerBonusSprites : IDisposable
{
    private const int DotSize = 64;
    private const int RingSize = 128;
    private const int GlowSize = 128;
    private const int PillSize = 64;
    private const float DotSolidRadius = 0.35f;
    private const float RingRadius = 0.9f;
    private const float RingHalfWidth = 0.08f;
    private const int AtlasPadding = 2;
    private const int AtlasMaxSize = 1024;

    private readonly List<Object> _owned = new List<Object>();

    /// <summary>가운데가 꽉 찬 부드러운 원 (불꽃·점·속도선).</summary>
    public Sprite SoftDot { get; }

    /// <summary>얇은 링 (충격파).</summary>
    public Sprite Ring { get; }

    /// <summary>가운데 밝고 바깥으로 사라지는 빛 (타이머 글로우·보스 피격 번쩍임).</summary>
    public Sprite Glow { get; }

    /// <summary>둥근 알약 (9-slice: 가로로 늘이면 양끝이 반원).</summary>
    public Sprite Pill { get; }

    /// <summary>스프라이트 4종을 공용 텍스처 한 장에 만든다.</summary>
    public TimerBonusSprites()
    {
        var sources = new[]
        {
            Draw(DotSize, r => Mathf.Clamp01((1f - r) / DotSolidRadius)),
            Draw(RingSize, r => Mathf.Clamp01(1f - Mathf.Abs(r - RingRadius) / RingHalfWidth)),
            Draw(GlowSize, r => { float k = Mathf.Clamp01(1f - r); return k * k; }),
            Draw(PillSize, r => Mathf.Clamp01((1f - r) * PillSize * 0.5f)),
        };
        var atlas = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "TimerBonus_SharedAtlas",
        };
        var rects = atlas.PackTextures(sources, AtlasPadding, AtlasMaxSize, false);
        foreach (var source in sources) Object.Destroy(source);
        _owned.Add(atlas);

        SoftDot = Slice(atlas, rects[0]);
        Ring = Slice(atlas, rects[1]);
        Glow = Slice(atlas, rects[2]);
        float half = PillSize * 0.5f - 1f;
        Pill = Slice(atlas, rects[3], new Vector4(half, half, half, half));
        atlas.Apply(false, true);
    }

    // 묶기 전 원본 한 장. PackTextures가 읽어야 하므로 읽기 가능 상태로 둔다.
    private static Texture2D Draw(int size, Func<float, float> alphaByRadius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TimerBonusLab_Generated" };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float a = alphaByRadius(Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    // 아틀라스 안 자리에서 원래와 같은 크기·피벗·9-slice의 스프라이트를 만든다.
    private Sprite Slice(Texture2D atlas, Rect uv, Vector4 border = default)
    {
        var rect = new Rect(uv.x * atlas.width, uv.y * atlas.height, uv.width * atlas.width, uv.height * atlas.height);
        var sprite = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        _owned.Add(sprite);
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
