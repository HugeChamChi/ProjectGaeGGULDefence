using System;
using TMPro;
using UnityEngine;
using static TimerBonusEase;

/// <summary>
/// 시안 D 스탬프 — 거대한 "+15"가 쾅 찍히며 화면이 흔들리고,
/// 조각으로 부서진 뒤 타이머로 빨려 들어간다. 조각이 닿을 때마다 시간이 차오른다.
/// </summary>
public sealed class TimerBonusStamp : ITimerBonusConcept
{
    private const int KeyHit = 100;
    private const int KeyBreak = 101;
    private const int KeyFull = 102;
    private const float StampBelowPx = 92f;
    private const float ShardStagger = 0.014f;
    private const float ShardFlySeconds = 0.24f;
    private const float ShakeSeconds = 0.34f;
    private const float SwellSeconds = 0.06f;
    private const float StampOutline = 0.25f;
    private static readonly Color Ink = new Color(0.24f, 0.13f, 0f);
    private static readonly Color Cream = new Color(1f, 0.945f, 0.77f);
    private static readonly Color LightGold = new Color(1f, 0.88f, 0.54f);
    private static readonly Color[] DustColors = { new Color(0.91f, 0.89f, 0.82f), new Color(1f, 0.773f, 0.227f), new Color(0.79f, 0.75f, 0.66f) };
    private static readonly Color[] ArriveColors = { LightGold };

    private TimerBonusLab _lab;
    private TextMeshProUGUI _stamp;
    private Color[] _shardBurst;
    private Action<Vector2> _onShardArrive;

    /// <inheritdoc />
    public string Name => "D 스탬프";

    /// <inheritdoc />
    public void Setup(TimerBonusLab lab)
    {
        _lab = lab;
        _stamp = lab.CreateText("Stamp", 80f, lab.Settings.GoldColor);
        _stamp.outlineWidth = StampOutline;
        _stamp.outlineColor = Ink;
        _shardBurst = new[] { lab.Settings.GoldColor, Color.white };
        _onShardArrive = OnShardArrive;
    }

    /// <inheritdoc />
    public void SetVisible(bool visible) => _stamp.gameObject.SetActive(visible);

    /// <inheritdoc />
    public void Render(float a, float step)
    {
        var tune = _lab.Settings.Stamp;
        float px = _lab.Px;
        float hit = tune.HitTime, brk = tune.BreakTime, arrive = tune.ArriveTime;
        int count = Mathf.Max(1, tune.ShardCount);
        float arriveSpan = ShardStagger * (count - 1);
        Vector2 timer = _lab.TimerPosition;
        Vector2 stampPos = timer + new Vector2(0f, -StampBelowPx * px);

        float applied = _lab.Bonus * OutCubic(Mathf.Clamp01((a - arrive) / (arriveSpan + 0.06f)));
        _lab.SetTimerValue(_lab.Now + applied);

        // 스탬프: 크게 → 쾅 (회전은 반시계 양수)
        if (a >= tune.FallStart && a < brk)
        {
            float sc, alpha = 1f, rot;
            if (a < hit)
            {
                float q = (a - tune.FallStart) / (hit - tune.FallStart), e = InQuad(q);
                sc = Mathf.Lerp(tune.StartScale, 1f, e);
                alpha = Mathf.Clamp01(q * 2.5f);
                rot = Mathf.Lerp(22f, 6f, e);
            }
            else
            {
                float d = Spring(a - hit, 28f, 7f);
                sc = 1f - 0.14f * d;
                rot = 6f - 3f * d;
                if (a > brk - SwellSeconds) sc *= 1f + 0.25f * InQuad((a - (brk - SwellSeconds)) / SwellSeconds);
            }
            TimerBonusLab.SetText(_stamp, _lab.BonusShortText);
            TimerBonusLab.Place(_stamp, stampPos, Vector2.one * sc, alpha, rot);
        }
        else TimerBonusLab.Hide(_stamp);

        if (a >= hit && _lab.Once(KeyHit))
        {
            _lab.Impact(1.5f);
            _lab.Ring(stampPos, 30f, 170f, Color.white, 0.42f);
            _lab.Ring(stampPos, 20f, 120f, _lab.Settings.GoldColor, 0.52f);
            _lab.Burst(stampPos + new Vector2(0f, -20f * px), 24, 160f, 480f, DustColors, TimerBonusParticles.Kind.Dot,
                gravity: 1100f, drag: 0.03f, size0: 1.5f, size1: 4f, life0: 0.36f, life1: 0.64f, angle: Mathf.PI * 0.5f, spread: 1.5f);
        }

        if (a >= brk && _lab.Once(KeyBreak))
        {
            _lab.Burst(stampPos, 14, 200f, 520f, _shardBurst, drag: 0.08f);
            float halfWidth = _lab.TimerSize.x / 3f;
            for (int i = 0; i < count; i++)
            {
                float ang = TimerBonusLab.Rnd(0f, Mathf.PI * 2f), speed = TimerBonusLab.Rnd(220f, 520f) * px;
                _lab.Emit(new TimerBonusParticles.Particle
                {
                    Kind = TimerBonusParticles.Kind.Home,
                    Position = stampPos + new Vector2(TimerBonusLab.Rnd(-40f, 40f), TimerBonusLab.Rnd(-20f, 20f)) * px,
                    Velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed,
                    Drag = 0.12f,
                    HomeDelay = arrive - brk - ShardFlySeconds + i * ShardStagger,
                    HomeDuration = ShardFlySeconds,
                    Target = timer + new Vector2(TimerBonusLab.Rnd(-halfWidth, halfWidth), TimerBonusLab.Rnd(-10f, 6f) * px),
                    Curve = TimerBonusLab.Rnd(-40f, 40f) * px,
                    Size = TimerBonusLab.Rnd(2.5f, 4.5f) * px,
                    Color = i % 3 == 0 ? Cream : _lab.Settings.GoldColor,
                    MaxLife = 2f,
                    OnArrive = _onShardArrive,
                });
            }
        }

        if (a >= hit && a < hit + ShakeSeconds)
        {
            float k = 1f - (a - hit) / ShakeSeconds;
            float amp = tune.ShakeAmplitude * k * k;
            _lab.AddShake(amp * Mathf.Sin(a * 900f), -amp * 0.7f * Mathf.Cos(a * 1300f));
        }

        // 충격에 타이머가 위로 튀고 눌린다.
        float spring = a >= hit ? Spring(a - hit, 24f, 8f) : 0f;
        _lab.TimerTransform(0f, 12f * spring, 1f + 0.12f * spring, 1f - 0.12f * spring, 0f);
        _lab.SetGold(a >= arrive - 0.04f ? (a < arrive + arriveSpan + 0.2f ? 1f : 1f - Mathf.Clamp01((a - arrive - arriveSpan - 0.2f) / 0.5f)) : 0f);
        _lab.SetFlash(a >= arrive + arriveSpan ? 0.8f * (1f - Mathf.Clamp01((a - arrive - arriveSpan) / 0.16f)) : 0f);

        if (a >= arrive + arriveSpan + 0.04f && _lab.Once(KeyFull))
        {
            _lab.Impact(0.7f);
            _lab.Ring(timer, 30f, 125f, _lab.Settings.GoldColor, 0.44f);
        }
    }

    private void OnShardArrive(Vector2 at)
    {
        _lab.Pulse = Mathf.Min(0.14f, _lab.Pulse + 0.03f);
        _lab.Burst(at, 3, 80f, 200f, ArriveColors, life0: 0.12f, life1: 0.22f);
    }
}
