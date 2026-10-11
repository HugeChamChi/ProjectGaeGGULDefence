using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 실험실용 월드 공간 글리치 조각 — 흰 사각 스프라이트 풀로 깜빡이며 좌우로 튀는 띠·블록을 그린다 (디시그망 글리치 빔 시안 E).
/// 무늬는 TickSeconds마다 조각별 시드 해시로 바뀐다 — 타이머 해킹 글리치(TimerBonusHack)와 같은 '툭툭 끊기는' 박자.
/// </summary>
public sealed class WorldGlitchBlocks : IDisposable
{
    private const float TickSeconds = 0.04f;
    private const float VisibleChance = 0.72f;
    private const float WidthJitterMin = 0.6f;
    private const float WidthJitterMax = 1.4f;
    private const int TexturePixels = 4;

    private sealed class Block
    {
        public Vector3 Position;
        public Vector2 Size;
        public Color Color;
        public float Life;
        public float MaxLife;
        public float Jitter;
        public int Seed;
        public SpriteRenderer Renderer;
    }

    private readonly Transform _parent;
    private readonly int _sortingOrder;
    private readonly Texture2D _texture;
    private readonly Sprite _sprite;
    private readonly List<Block> _active = new List<Block>();
    private readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();
    private int _seed;

    /// <summary>parent 아래에 조각을 만든다. 1×1 월드 크기 사각형을 Size로 늘려 쓴다.</summary>
    public WorldGlitchBlocks(Transform parent, int sortingOrder)
    {
        _parent = parent;
        _sortingOrder = sortingOrder;
        _texture = new Texture2D(TexturePixels, TexturePixels, TextureFormat.RGBA32, false)
        {
            name = "GlitchBlock",
            filterMode = FilterMode.Point,
        };
        var pixels = new Color32[TexturePixels * TexturePixels];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        _texture.SetPixels32(pixels);
        _texture.Apply(false, true);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, TexturePixels, TexturePixels), new Vector2(0.5f, 0.5f), TexturePixels);
    }

    /// <summary>조각 하나 — size는 월드 가로·세로, jitter는 좌우로 튀는 최대 폭(월드).</summary>
    public void Spawn(Vector3 position, Vector2 size, Color color, float life, float jitter)
    {
        var sr = _free.Count > 0 ? _free.Pop() : Create();
        sr.enabled = true;
        _active.Add(new Block
        {
            Position = position,
            Size = size,
            Color = color,
            MaxLife = Mathf.Max(0.01f, life),
            Jitter = jitter,
            Seed = ++_seed,
            Renderer = sr,
        });
    }

    /// <summary>dt초 진행 후 그린다.</summary>
    public void Tick(float dt)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var b = _active[i];
            b.Life += dt;
            if (b.Life >= b.MaxLife)
            {
                Return(b.Renderer);
                _active.RemoveAt(i);
                continue;
            }
            int tick = Mathf.FloorToInt(b.Life / TickSeconds);
            bool visible = Hash(b.Seed, tick) < VisibleChance;
            float k = 1f - b.Life / b.MaxLife;
            float dx = (Hash(b.Seed + 101, tick) - 0.5f) * 2f * b.Jitter;
            float sx = Mathf.Lerp(WidthJitterMin, WidthJitterMax, Hash(b.Seed + 202, tick));
            var t = b.Renderer.transform;
            t.position = b.Position + new Vector3(dx, 0f, 0f);
            t.localScale = new Vector3(b.Size.x * sx, b.Size.y, 1f);
            var c = b.Color;
            c.a *= visible ? k : 0f;
            b.Renderer.color = c;
        }
    }

    /// <summary>모두 지운다.</summary>
    public void Clear()
    {
        foreach (var b in _active) Return(b.Renderer);
        _active.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Clear();
        if (_sprite != null) Object.Destroy(_sprite);
        if (_texture != null) Object.Destroy(_texture);
    }

    /// <summary>같은 (a, b)면 항상 같은 0~1 값 — 히트스톱·슬로모션에서도 무늬가 안정적이다.</summary>
    internal static float Hash(int a, int b)
    {
        float v = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    private SpriteRenderer Create()
    {
        var go = new GameObject("GlitchBlock", typeof(SpriteRenderer));
        go.transform.SetParent(_parent, false);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = _sprite;
        sr.sortingOrder = _sortingOrder;
        return sr;
    }

    private void Return(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.enabled = false;
        _free.Push(sr);
    }
}
