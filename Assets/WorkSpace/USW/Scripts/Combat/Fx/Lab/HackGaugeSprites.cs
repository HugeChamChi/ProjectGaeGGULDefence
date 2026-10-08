using System;
using UnityEngine;

/// <summary>FxLab_HackBloom 스택 UI 시안용 절차 생성 스프라이트 (아트가 나오면 교체). 한 번 만들어 재사용한다.</summary>
public static class HackGaugeSprites
{
    private const int Size = 128;
    private const int Segments = 10;
    private const float SegmentGap = .09f;
    private static Sprite _ring, _disc, _hex, _hexLine, _thinRing, _segmentRing, _softDisc, _chargeRing;

    /// <summary>독립 계기판의 굵은 10칸 충전 띠.</summary>
    public static Sprite ChargeRing => _chargeRing != null ? _chargeRing : _chargeRing = MakeXY("HackChargeRing", (x, y) =>
    {
        float t = Mathf.Repeat(Mathf.Atan2(x, y) / (2f * Mathf.PI), 1f) * Segments;
        float edge = Mathf.Min(t - Mathf.Floor(t), Mathf.Ceil(t) - t);
        return Band(Mathf.Sqrt(x * x + y * y), .69f, .93f) * Mathf.Clamp01((edge - .035f) / .035f);
    });

    /// <summary>초상화에 딱 맞는 얇은 원형 띠 (안쪽 0.84 ~ 바깥 0.98).</summary>
    public static Sprite ThinRing => _thinRing != null ? _thinRing : _thinRing = Make("HackThinRing", r => Band(r, .84f, .98f));
    /// <summary>10칸으로 나뉜 얇은 원형 띠 — 12시 방향에서 칸 사이가 갈라진다.</summary>
    public static Sprite SegmentRing => _segmentRing != null ? _segmentRing : _segmentRing = MakeXY("HackSegmentRing", (x, y) =>
    {
        float t = Mathf.Repeat(Mathf.Atan2(x, y) / (2f * Mathf.PI), 1f) * Segments;
        float edge = Mathf.Min(t - Mathf.Floor(t), Mathf.Ceil(t) - t);
        return Band(Mathf.Sqrt(x * x + y * y), .84f, .98f) * Mathf.Clamp01((edge - SegmentGap * .5f) / .04f);
    });
    /// <summary>가운데가 진하고 바깥으로 부드럽게 사라지는 빛.</summary>
    public static Sprite SoftDisc => _softDisc != null ? _softDisc : _softDisc = Make("HackSoftDisc", r => Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f));

    /// <summary>얇은 원형 띠 (원형 게이지 바탕·채움용, Filled Radial360에 쓴다).</summary>
    public static Sprite Ring => _ring != null ? _ring : _ring = Make("HackRing", r => Band(r, .64f, .92f));
    /// <summary>꽉 찬 원.</summary>
    public static Sprite Disc => _disc != null ? _disc : _disc = Make("HackDisc", r => Edge(.98f - r));
    /// <summary>꽉 찬 육각형 (디버프 칸 아이콘).</summary>
    public static Sprite Hex => _hex != null ? _hex : _hex = MakeXY("HackHex", (x, y) => Edge(.95f - HexDistance(x, y)));
    /// <summary>육각형 테두리.</summary>
    public static Sprite HexLine => _hexLine != null ? _hexLine : _hexLine = MakeXY("HackHexLine", (x, y) => Band(HexDistance(x, y), .76f, .95f));

    private static float HexDistance(float x, float y)
    {
        x = Mathf.Abs(x); y = Mathf.Abs(y);
        return Mathf.Max(x * .866f + y * .5f, y);
    }

    private static float Edge(float d) => Mathf.Clamp01(d / .025f);
    private static float Band(float d, float inner, float outer) => Edge(d - inner) * Edge(outer - d);

    private static Sprite Make(string name, Func<float, float> alpha)
        => MakeXY(name, (x, y) => alpha(Mathf.Sqrt(x * x + y * y)));

    private static Sprite MakeXY(string name, Func<float, float, float> alpha)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[Size * Size];
        for (int py = 0; py < Size; py++)
        for (int px = 0; px < Size; px++)
        {
            float x = (px + .5f) / Size * 2f - 1f, y = (py + .5f) / Size * 2f - 1f;
            pixels[py * Size + px] = new Color32(255, 255, 255, (byte)(alpha(x, y) * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), 100f);
        sprite.name = name;
        return sprite;
    }
}
