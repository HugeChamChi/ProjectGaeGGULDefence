using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 실험실용 UI 파티클 — Image 풀로 불꽃·점·링·속도선·흡수 조각을 그린다.
/// 단위는 캔버스 단위·초. 감쇠(Drag)는 HTML 시안처럼 60fps 프레임당 비율.
/// </summary>
public sealed class TimerBonusParticles
{
    /// <summary>그리는 방식.</summary>
    public enum Kind { Spark, Dot, Ring, Streak, Home }

    /// <summary>파티클 하나.</summary>
    public sealed class Particle
    {
        public Kind Kind;
        public Vector2 Position;
        public Vector2 Velocity;
        /// <summary>아래로 당기는 가속도.</summary>
        public float Gravity;
        public float Drag;
        /// <summary>Spark·Streak 두께, Dot·Home 반지름.</summary>
        public float Size;
        public float MaxLife = 0.5f;
        public float Alpha = 1f;
        public Color Color = Color.white;
        public float RadiusFrom;
        public float RadiusTo;
        public float Length;
        /// <summary>Home: 흩어진 뒤 목표로 날아가기 시작하는 나이(초).</summary>
        public float HomeDelay;
        public float HomeDuration = 0.24f;
        public Vector2 Target;
        /// <summary>Home: 날아가는 길의 가로 휨.</summary>
        public float Curve;
        public Action<Vector2> OnArrive;

        internal float Life;
        internal bool Homing;
        internal Vector2 HomeStart;
        internal Image Image;
    }

    private const float FrameRef = 60f;
    private const float SparkTail = 0.03f;
    private const float HomeTail = 0.016f;

    private readonly RectTransform _layer;
    private readonly Sprite _dot;
    private readonly Sprite _ring;
    private readonly List<Particle> _active = new List<Particle>();
    private readonly Stack<Image> _free = new Stack<Image>();

    /// <summary>layer 아래에 파티클 Image를 만든다.</summary>
    public TimerBonusParticles(RectTransform layer, Sprite dot, Sprite ring)
    {
        _layer = layer;
        _dot = dot;
        _ring = ring;
    }

    /// <summary>파티클 추가.</summary>
    public void Emit(Particle p)
    {
        p.Life = 0f;
        p.Homing = false;
        p.Image = Rent(p.Kind);
        _active.Add(p);
    }

    /// <summary>모두 지운다.</summary>
    public void Clear()
    {
        foreach (var p in _active) Return(p.Image);
        _active.Clear();
    }

    /// <summary>step초 진행 후 그린다 (step 0 = 멈춘 채 그리기).</summary>
    public void Tick(float step)
    {
        // 뒤에서부터: 도착 콜백이 새 파티클을 붙여도 이번 프레임 순회에 섞이지 않는다.
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var p = _active[i];
            bool arrived = step > 0f && Advance(p, step);
            if (arrived || p.Life >= p.MaxLife)
            {
                Return(p.Image);
                _active.RemoveAt(i);
                continue;
            }
            Draw(p);
        }
    }

    private static bool Advance(Particle p, float step)
    {
        p.Life += step;
        if (p.Kind == Kind.Home && p.Life >= p.HomeDelay)
        {
            if (!p.Homing) { p.Homing = true; p.HomeStart = p.Position; }
            float q = Mathf.Clamp01((p.Life - p.HomeDelay) / Mathf.Max(0.0001f, p.HomeDuration));
            float e = TimerBonusEase.InCubic(q);
            Vector2 prev = p.Position;
            p.Position = Vector2.LerpUnclamped(p.HomeStart, p.Target, e) + new Vector2(Mathf.Sin(q * Mathf.PI) * p.Curve, 0f);
            p.Velocity = (p.Position - prev) / step;
            if (q < 1f) return false;
            p.OnArrive?.Invoke(p.Position);
            return true;
        }
        float d = Mathf.Pow(1f - p.Drag, step * FrameRef);
        p.Velocity *= d;
        p.Velocity.y -= p.Gravity * step;
        p.Position += p.Velocity * step;
        return false;
    }

    private void Draw(Particle p)
    {
        float k = p.Life / p.MaxLife;
        var rt = p.Image.rectTransform;
        float alpha = (1f - k) * p.Alpha;
        switch (p.Kind)
        {
            case Kind.Spark:
                Stretch(rt, p.Position, p.Velocity, SparkTail, p.Size, p.Size);
                break;
            case Kind.Home:
                Stretch(rt, p.Position, p.Velocity, HomeTail, p.Size * 2f, p.Size * 2f);
                alpha = p.Alpha;
                break;
            case Kind.Dot:
            {
                float s = p.Size * 2f * (1f - k * 0.5f);
                rt.sizeDelta = new Vector2(s, s);
                rt.anchoredPosition = p.Position;
                rt.localRotation = Quaternion.identity;
                break;
            }
            case Kind.Ring:
            {
                float r = Mathf.Lerp(p.RadiusFrom, p.RadiusTo, TimerBonusEase.OutCubic(k));
                rt.sizeDelta = new Vector2(r * 2f, r * 2f);
                rt.anchoredPosition = p.Position;
                rt.localRotation = Quaternion.identity;
                break;
            }
            case Kind.Streak:
                rt.sizeDelta = new Vector2(p.Length, p.Size);
                rt.anchoredPosition = p.Position + new Vector2(p.Length * 0.5f, 0f);
                rt.localRotation = Quaternion.identity;
                break;
        }
        var c = p.Color;
        p.Image.color = new Color(c.r, c.g, c.b, c.a * alpha);
    }

    // 속도 방향으로 늘린 막대 — 머리는 현재 위치, 꼬리는 지나온 쪽.
    private static void Stretch(RectTransform rt, Vector2 pos, Vector2 vel, float tail, float minLength, float thickness)
    {
        float speed = vel.magnitude;
        float len = Mathf.Max(minLength, speed * tail);
        rt.sizeDelta = new Vector2(len, thickness);
        Vector2 dir = speed > 0.001f ? vel / speed : Vector2.right;
        rt.anchoredPosition = pos - dir * (len * 0.5f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    private Image Rent(Kind kind)
    {
        Image img;
        if (_free.Count > 0) img = _free.Pop();
        else
        {
            var go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_layer, false);
            img = go.GetComponent<Image>();
            img.raycastTarget = false;
        }
        img.sprite = kind == Kind.Ring ? _ring : _dot;
        img.enabled = true;
        img.rectTransform.SetAsLastSibling();
        return img;
    }

    private void Return(Image img)
    {
        if (img == null) return;
        img.enabled = false;
        _free.Push(img);
    }
}
