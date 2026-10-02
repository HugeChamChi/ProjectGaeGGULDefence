using TMPro;
using UnityEngine;
using static TimerBonusEase;

/// <summary>
/// 시안 A 합체 — 보스 자리에서 "+15.00" 칩이 가속하며 날아와 타이머에 박히고,
/// 두 숫자가 한 덩어리로 찌그러졌다 튕겨 나오며 합산값이 된다.
/// </summary>
public sealed class TimerBonusFusion : ITimerBonusConcept
{
    private const int KeyImpact = 100;
    private const float ChipPopSeconds = 0.09f;
    private const float ArcPx = 30f;
    private const float AnticipationSeconds = 0.11f;
    private const float GoldHoldSeconds = 0.22f;
    private const float GoldFadeSeconds = 0.7f;
    private const float FlashSeconds = 0.13f;
    private const float GhostSeconds = 0.82f;
    private static readonly Color Cream = new Color(1f, 0.945f, 0.77f);
    private static readonly Color LightGold = new Color(1f, 0.88f, 0.54f);

    private TimerBonusLab _lab;
    private TextMeshProUGUI _chip;
    private TextMeshProUGUI _ghost;
    private Color[] _impactColors;
    private Color[] _trailColors;

    /// <inheritdoc />
    public string Name => "A 합체";

    /// <inheritdoc />
    public void Setup(TimerBonusLab lab)
    {
        _lab = lab;
        var gold = lab.Settings.GoldColor;
        _chip = lab.CreateText("FusionChip", 28f, gold);
        _ghost = lab.CreateText("FusionGhost", 18f, gold);
        _impactColors = new[] { gold, Color.white, LightGold };
        _trailColors = new[] { gold, Cream };
    }

    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        _chip.gameObject.SetActive(visible);
        _ghost.gameObject.SetActive(visible);
    }

    /// <inheritdoc />
    public void Render(float a, float step)
    {
        var tune = _lab.Settings.Fusion;
        float fly = tune.FlySeconds;
        float px = _lab.Px;
        Vector2 timer = _lab.TimerPosition;

        float applied = a < fly ? 0f : _lab.Bonus * OutExpo(Mathf.Clamp01((a - fly) / tune.CountSeconds));
        _lab.SetTimerValue(_lab.Now + applied);

        // 칩 비행: 처음엔 톡 튀어나오고, 갈수록 가속하며 세로로 늘어난다.
        if (a >= 0f && a < fly)
        {
            Vector2 boss = _lab.BossCenter;
            float p = a / fly, e = InCubic(p), pop = OutBack(Mathf.Clamp01(a / ChipPopSeconds));
            var pos = new Vector2(
                Mathf.Lerp(boss.x, timer.x, e) + Mathf.Sin(p * Mathf.PI) * ArcPx * px,
                Mathf.Lerp(boss.y + 10f * px, timer.y, e));
            TimerBonusLab.SetText(_chip, _lab.BonusText);
            TimerBonusLab.Place(_chip, pos, new Vector2((1f - 0.3f * e) * pop, (1f + 0.65f * e) * pop), 1f);
            if (step > 0f)
            {
                for (int i = 0; i < _trailColors.Length; i++)
                {
                    _lab.Emit(new TimerBonusParticles.Particle
                    {
                        Kind = TimerBonusParticles.Kind.Dot,
                        Position = pos + new Vector2(TimerBonusLab.Rnd(-8f, 8f), -TimerBonusLab.Rnd(0f, 10f)) * px,
                        Velocity = new Vector2(0f, -TimerBonusLab.Rnd(40f, 120f) * px),
                        Size = TimerBonusLab.Rnd(1.5f, 3.5f) * px,
                        MaxLife = TimerBonusLab.Rnd(0.18f, 0.32f),
                        Color = _trailColors[i],
                    });
                }
            }
        }
        else TimerBonusLab.Hide(_chip);

        // 타이머: 칩이 닿기 직전 아래로 살짝 끌려갔다가, 충돌하면 가로로 퍼지며 튕긴다.
        float sx = 1f, sy = 1f, ty = 0f;
        if (a > fly - AnticipationSeconds && a < fly)
        {
            float q = (a - (fly - AnticipationSeconds)) / AnticipationSeconds;
            sy = 1f + 0.12f * q;
            sx = 1f - 0.05f * q;
            ty = -5f * q;
        }
        if (a >= fly)
        {
            float d = Spring(a - fly, 22f, 5.5f);
            sx = 1f + tune.SquashX * d;
            sy = 1f - tune.SquashY * d;
        }
        _lab.TimerTransform(0f, ty, sx, sy, 0f);
        _lab.SetGold(a >= fly ? 1f - Mathf.Clamp01((a - fly - GoldHoldSeconds) / GoldFadeSeconds) : 0f);
        _lab.SetFlash(a >= fly ? 1f - Mathf.Clamp01((a - fly) / FlashSeconds) : 0f);

        if (a >= fly && _lab.Once(KeyImpact))
        {
            _lab.Impact(1.1f);
            _lab.Burst(timer, 28, 260f, 720f, _impactColors, drag: 0.07f, size0: 2f, size1: 3.5f);
            _lab.Ring(timer, 24f, 120f, _lab.Settings.GoldColor, 0.46f);
            _lab.Ring(timer, 10f, 70f, Color.white, 0.26f);
        }

        // 합쳐진 뒤 오른쪽 위로 "+15.00"이 떠오르며 사라진다 (몇 초 붙었는지 확인용).
        if (a >= fly && a < fly + GhostSeconds)
        {
            float g = (a - fly) / GhostSeconds;
            TimerBonusLab.SetText(_ghost, _lab.BonusText);
            var pos = new Vector2(timer.x + _lab.TimerSize.x * 0.5f + 24f * px, timer.y + 18f * px + 26f * px * OutCubic(g));
            TimerBonusLab.Place(_ghost, pos, Vector2.one * (0.8f + 0.2f * OutBack(Mathf.Clamp01(g * 4f))), 1f - InQuad(g));
        }
        else TimerBonusLab.Hide(_ghost);
    }
}
