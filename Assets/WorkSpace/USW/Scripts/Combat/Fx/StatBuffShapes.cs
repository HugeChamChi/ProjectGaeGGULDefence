using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// StatBuffFx가 쓰는 그림을 코드로 만들어 캐시한다 (외부 그림 없음). 모두 흰색 + 알파 모양이고 색은 렌더러 색으로 칠한다.
/// 모양은 부호 거리(안쪽 음수, 텍스처 반 변 = 1)로 정의해서 채움과 외곽선을 같은 식에서 뽑는다.
/// 채움은 위쪽이 살짝 밝다 (버프예시2의 납작한 세모처럼 밋밋하지 않게).
/// </summary>
public static class StatBuffShapes
{
    /// <summary>채움 / 외곽선 한 쌍. 외곽선이 없는 모양은 Outline = null.</summary>
    public readonly struct Pair
    {
        public readonly Sprite Fill;
        public readonly Sprite Outline;
        public Pair(Sprite fill, Sprite outline) { Fill = fill; Outline = outline; }
    }

    private const int ShapeSize = 96;
    private const float OutlineWidth = 0.15f;
    private const float ArcSpanDegrees = 140f;

    /// <summary>소용돌이 텍스처 안의 원 반지름 (스프라이트 한 변 = 1).</summary>
    public const float ArcTextureRadius = 0.42f;
    /// <summary>소용돌이 호가 차지하는 각도.</summary>
    public const float ArcSpan = ArcSpanDegrees;

    private static readonly Dictionary<StatBuffShape, Pair> Cache = new Dictionary<StatBuffShape, Pair>();
    private static Sprite _dot, _streak, _column, _arc, _ring, _pip;

    /// <summary>부드러운 원 (번짐·발밑 오라).</summary>
    public static Sprite Dot => _dot != null ? _dot : _dot = Make(64, 64, (x, y) => Mathf.Exp(-(x * x + y * y) * 4.5f) * Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 0.5f);

    /// <summary>가는 세로 줄 (속도선). 피벗 = 아래, 위로 갈수록 흐려진다.</summary>
    public static Sprite Streak => _streak != null ? _streak : _streak = Make(16, 128, (x, y) =>
    {
        float v = (y + 1f) * 0.5f;
        return Mathf.Exp(-x * x * 6f) * Mathf.Pow(1f - v, 1.2f) * Mathf.Clamp01(v / 0.08f);
    }, 0f);

    /// <summary>빛기둥. 피벗 = 아래, 아래가 밝고 위로 흐려진다.</summary>
    public static Sprite Column => _column != null ? _column : _column = Make(64, 128, (x, y) =>
    {
        float v = (y + 1f) * 0.5f;
        return Mathf.Exp(-x * x * 3f) * Mathf.Pow(1f - v, 1.6f) * Mathf.Clamp01(v / 0.05f);
    }, 0f);

    /// <summary>초승달 호 — 0°에서 ArcSpan까지, 끝(진행 방향)이 굵고 밝다. 피벗 = 원 중심.</summary>
    public static Sprite Arc => _arc != null ? _arc : _arc = Make(128, 128, ArcAlpha, 0.5f);

    /// <summary>가는 고리 (시작 고리).</summary>
    public static Sprite Ring => _ring != null ? _ring : _ring = Make(128, 128, (x, y) =>
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float d = (r - 0.86f) / 0.07f;
        return Mathf.Exp(-d * d) * (r < 1f ? 1f : 0f);
    }, 0.5f);

    /// <summary>시간 눈금 한 칸 (둥근 점).</summary>
    public static Sprite Pip => _pip != null ? _pip : _pip = Make(32, 32, (x, y) => Edge(0.75f - Mathf.Sqrt(x * x + y * y), 32), 0.5f);

    /// <summary>모양의 채움 / 외곽선 스프라이트.</summary>
    public static Pair Get(StatBuffShape shape)
    {
        if (shape == StatBuffShape.None) shape = StatBuffShape.Triangle;
        if (Cache.TryGetValue(shape, out var pair)) return pair;

        switch (shape)
        {
            case StatBuffShape.Sparkle:
                pair = new Pair(Make(64, 64, SparkleAlpha, 0.5f), null);
                break;
            case StatBuffShape.Blob:
                pair = new Pair(Make(64, 64, (x, y) =>
                {
                    float r2 = x * x + y * y;
                    return Mathf.Clamp01(Mathf.Exp(-r2 * 3.2f) * 1.25f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 4f);
                }, 0.5f, Shade), null);
                break;
            default:
                Func<float, float, float> sdf = Distance(shape);
                pair = new Pair(
                    Make(ShapeSize, ShapeSize, (x, y) => Edge(-sdf(x, y), ShapeSize), 0.5f, Shade),
                    Make(ShapeSize, ShapeSize, (x, y) => Edge(-(sdf(x, y) - OutlineWidth), ShapeSize), 0.5f));
                break;
        }
        Cache[shape] = pair;
        return pair;
    }

    // ── 모양 (부호 거리) ─────────────────────────────────

    private static readonly Vector2[] ArrowPoly =
    {
        new Vector2(0f, 0.82f), new Vector2(0.62f, 0.18f), new Vector2(0.24f, 0.18f), new Vector2(0.24f, -0.78f),
        new Vector2(-0.24f, -0.78f), new Vector2(-0.24f, 0.18f), new Vector2(-0.62f, 0.18f),
    };

    private static readonly Vector2[] ChevronPoly =
    {
        new Vector2(-0.8f, -0.12f), new Vector2(0f, 0.62f), new Vector2(0.8f, -0.12f),
        new Vector2(0.8f, -0.55f), new Vector2(0f, 0.18f), new Vector2(-0.8f, -0.55f),
    };

    private static readonly Vector2[] BoltPoly =
    {
        new Vector2(0.2f, 0.85f), new Vector2(-0.5f, -0.05f), new Vector2(-0.04f, -0.05f), new Vector2(-0.22f, -0.85f),
        new Vector2(0.52f, 0.12f), new Vector2(0.06f, 0.12f),
    };

    private static readonly Vector2[] ShardPoly =
    {
        new Vector2(0f, 0.85f), new Vector2(0.3f, 0.05f), new Vector2(0.05f, -0.75f), new Vector2(-0.24f, -0.05f),
    };

    private static readonly Vector2[] PlusPoly = PlusPoints(0.24f, 0.78f);
    private static readonly Vector2[] CrossPoly = Rotate(PlusPoints(0.22f, 0.8f), 45f);
    private static readonly Vector2[] StarPoly = StarPoints(0.86f, 0.38f);

    private static Func<float, float, float> Distance(StatBuffShape shape)
    {
        switch (shape)
        {
            case StatBuffShape.Chevron: return (x, y) => Poly(ChevronPoly, x, y);
            case StatBuffShape.Arrow: return (x, y) => Poly(ArrowPoly, x, y);
            case StatBuffShape.Bolt: return (x, y) => Poly(BoltPoly, x, y);
            case StatBuffShape.Star: return (x, y) => Poly(StarPoly, x, y);
            case StatBuffShape.Plus: return (x, y) => Poly(PlusPoly, x, y);
            case StatBuffShape.Cross: return (x, y) => Poly(CrossPoly, x, y);
            case StatBuffShape.Shard: return (x, y) => Poly(ShardPoly, x, y);
            case StatBuffShape.Leaf:
                // 두 원의 교집합 (위아래로 뾰족한 잎)
                return (x, y) => Mathf.Max(Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + y * y) - 0.78f,
                                           Mathf.Sqrt((x + 0.5f) * (x + 0.5f) + y * y) - 0.78f);
            case StatBuffShape.TripleChevron:
                return (x, y) =>
                {
                    float d = float.MaxValue;
                    for (int i = -1; i <= 1; i++) d = Mathf.Min(d, Poly(ChevronPoly, x / 0.62f, (y - i * 0.5f) / 0.62f) * 0.62f);
                    return d;
                };
            case StatBuffShape.FanArrows:
                return (x, y) =>
                {
                    float d = float.MaxValue;
                    for (int i = -1; i <= 1; i++)
                    {
                        // 아래쪽 한 점을 축으로 부채꼴로 벌린다
                        float a = i * 30f * Mathf.Deg2Rad;
                        float px = x, py = y + 0.7f;
                        float rx = px * Mathf.Cos(a) - py * Mathf.Sin(a);
                        float ry = px * Mathf.Sin(a) + py * Mathf.Cos(a) - 0.7f;
                        d = Mathf.Min(d, Poly(ArrowPoly, rx / 0.52f, (ry - 0.25f) / 0.52f) * 0.52f);
                    }
                    return d;
                };
            default:
                return RoundedTriangle;
        }
    }

    // 둥근 모서리 정삼각형 (위를 가리킴) — 버프예시2의 납작한 민트색 세모
    private static float RoundedTriangle(float x, float y)
    {
        const float k = 1.7320508f;
        const float r = 0.62f;
        float px = Mathf.Abs(x) - r;
        float py = (y + 0.1f) + r / k;
        if (px + k * py > 0f)
        {
            float nx = (px - k * py) * 0.5f;
            float ny = (-k * px - py) * 0.5f;
            px = nx; py = ny;
        }
        px -= Mathf.Clamp(px, -2f * r, 0f);
        float d = -Mathf.Sqrt(px * px + py * py) * Mathf.Sign(py);
        return d - 0.08f;
    }

    // 다각형 부호 거리 — 변까지 최소 거리 + 짝홀 규칙 안팎 (오목해도 된다)
    private static float Poly(Vector2[] v, float x, float y)
    {
        float d = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            Vector2 a = v[j], b = v[i];
            float ex = b.x - a.x, ey = b.y - a.y;
            float wx = x - a.x, wy = y - a.y;
            float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            float dx = wx - ex * t, dy = wy - ey * t;
            d = Mathf.Min(d, dx * dx + dy * dy);
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        d = Mathf.Sqrt(d);
        return inside ? -d : d;
    }

    private static Vector2[] PlusPoints(float half, float len) => new[]
    {
        new Vector2(-half, len), new Vector2(half, len), new Vector2(half, half), new Vector2(len, half),
        new Vector2(len, -half), new Vector2(half, -half), new Vector2(half, -len), new Vector2(-half, -len),
        new Vector2(-half, -half), new Vector2(-len, -half), new Vector2(-len, half), new Vector2(-half, half),
    };

    private static Vector2[] StarPoints(float outer, float inner)
    {
        var p = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = (90f + i * 36f) * Mathf.Deg2Rad;
            float r = i % 2 == 0 ? outer : inner;
            p[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.05f);
        }
        return p;
    }

    private static Vector2[] Rotate(Vector2[] points, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
        var r = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) r[i] = new Vector2(points[i].x * c - points[i].y * s, points[i].x * s + points[i].y * c);
        return r;
    }

    private static float SparkleAlpha(float x, float y)
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float star = Mathf.Max(Mathf.Clamp01(1f - (ax * 7f + ay)), Mathf.Clamp01(1f - (ay * 7f + ax)));
        float core = Mathf.Exp(-(x * x + y * y) * 30f);
        return Mathf.Clamp01(star + core);
    }

    private static float ArcAlpha(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y) * 0.5f;
        float theta = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        if (theta < 0f) theta += 360f;
        if (theta > ArcSpanDegrees) return 0f;
        float t = theta / ArcSpanDegrees;
        float thick = 0.055f * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.6f) * Mathf.Lerp(0.5f, 1.2f, t);
        float d = Mathf.Abs(r - ArcTextureRadius);
        float fill = Mathf.Clamp01((thick - d) / 0.008f);
        float glow = 0.35f * Mathf.Exp(-Mathf.Pow(d / (thick * 2.4f + 0.001f), 2f)) * Mathf.Sin(t * Mathf.PI);
        return Mathf.Clamp01(fill * Mathf.Lerp(0.55f, 1f, t) + glow);
    }

    private static float Shade(float x, float y) => Mathf.Lerp(0.8f, 1f, Mathf.Clamp01((y + 0.6f) / 1.2f));

    private static float Edge(float signedInside, int size) => Mathf.Clamp01(signedInside * size * 0.5f + 0.5f);

    private static Sprite Make(int w, int h, Func<float, float, float> alpha, float pivotY, Func<float, float, float> shade = null)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var px = new Color32[w * h];
        for (int j = 0; j < h; j++)
        {
            float y = (j + 0.5f) / h * 2f - 1f;
            for (int i = 0; i < w; i++)
            {
                float x = (i + 0.5f) / w * 2f - 1f;
                float a = Mathf.Clamp01(alpha(x, y));
                byte v = (byte)(255f * (shade != null ? Mathf.Clamp01(shade(x, y)) : 1f));
                px[j * w + i] = new Color32(v, v, v, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, pivotY), Mathf.Max(w, h));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
