using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 실험실에서 쓰는 절차 생성 스프라이트 (부드러운 점·링·글로우).
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

    private readonly List<Object> _owned = new List<Object>();

    /// <summary>가운데가 꽉 찬 부드러운 원 (불꽃·점·속도선).</summary>
    public Sprite SoftDot { get; }

    /// <summary>얇은 링 (충격파).</summary>
    public Sprite Ring { get; }

    /// <summary>가운데 밝고 바깥으로 사라지는 빛 (타이머 글로우·보스 피격 번쩍임).</summary>
    public Sprite Glow { get; }

    /// <summary>둥근 알약 (9-slice: 가로로 늘이면 양끝이 반원).</summary>
    public Sprite Pill { get; }

    /// <summary>스프라이트 4종을 만든다.</summary>
    public TimerBonusSprites()
    {
        SoftDot = Make(DotSize, r => Mathf.Clamp01((1f - r) / DotSolidRadius));
        Ring = Make(RingSize, r => Mathf.Clamp01(1f - Mathf.Abs(r - RingRadius) / RingHalfWidth));
        Glow = Make(GlowSize, r => { float k = Mathf.Clamp01(1f - r); return k * k; });
        float half = PillSize * 0.5f - 1f;
        Pill = Make(PillSize, r => Mathf.Clamp01((1f - r) * PillSize * 0.5f), new Vector4(half, half, half, half));
    }

    private Sprite Make(int size, Func<float, float> alphaByRadius, Vector4 border = default)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "TimerBonusLab_Generated",
        };
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
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
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
