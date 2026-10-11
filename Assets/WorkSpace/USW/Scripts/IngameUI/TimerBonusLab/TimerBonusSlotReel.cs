using TMPro;
using UnityEngine;
using static TimerBonusEase;

/// <summary>
/// 시안 B 슬롯 릴 — "+15.00" 칩이 타이머로 빨려 들어가면 자릿수가 슬롯처럼 돌다가
/// 왼쪽부터 탁·탁·탁·탁 멈추고, 마지막에 크게 튕긴다.
/// 회전 중에도 실제 시간은 흐르므로, 마지막 릴이 멈추는 순간의 (남은 시간 + 보너스)에 착지한다.
/// </summary>
public sealed class TimerBonusSlotReel : ITimerBonusConcept
{
    private const int KeyStopBase = 100;
    private const int KeyDone = 110;
    private const int KeySuck = 111;
    private const int ReelCount = 4;
    private const int ExtraSpins = 2;                 // 최소 회전 바퀴 (뒤 릴일수록 한 바퀴씩 더)
    private const float BlurPower = 1.4f;
    private const float ChipInSeconds = 0.15f;
    private const float ChipHoldEnd = 0.26f;
    private const float ChipSuckEnd = 0.4f;
    private const float SuckBurstAt = 0.33f;
    private const float ChipBelowPx = 62f;
    private const float ChipEnterPx = 150f;
    private static readonly Color[] StopColors = { new Color(1f, 0.773f, 0.227f), Color.white };

    private TimerBonusLab _lab;
    private TextMeshProUGUI _chip;
    private Color[] _goldOnly;

    /// <inheritdoc />
    public string Name => "B 슬롯 릴";

    /// <inheritdoc />
    public void Setup(TimerBonusLab lab)
    {
        _lab = lab;
        _chip = lab.CreateText("ReelChip", 28f, lab.Settings.GoldColor);
        _goldOnly = new[] { lab.Settings.GoldColor };
    }

    /// <inheritdoc />
    public void SetVisible(bool visible) => _chip.gameObject.SetActive(visible);

    /// <inheritdoc />
    public void Render(float a, float step)
    {
        var tune = _lab.Settings.SlotReel;
        float px = _lab.Px;
        float start = tune.StartDelay;
        float end = start + tune.SpinSeconds + (ReelCount - 1) * tune.Stagger;
        Vector2 timer = _lab.TimerPosition;
        var digits = _lab.MainDigits;

        if (a < start || a >= end) _lab.SetTimerValue(_lab.Now + (a >= end ? _lab.Bonus : 0f));
        else
        {
            string from = TimerBonusDigits.Format(_lab.RemainingAt(start));
            string to = TimerBonusDigits.Format(_lab.RemainingAt(end) + _lab.Bonus);
            for (int j = 0; j < ReelCount; j++)
            {
                int ci = digits.CharIndexOf(j);
                int f = from[ci] - '0', target = to[ci] - '0';
                int distance = ((target - f) % 10 + 10) % 10 + 10 * (ExtraSpins + j);
                float stopAt = start + tune.SpinSeconds + j * tune.Stagger;
                float p = Mathf.Clamp01((a - start) / (stopAt - start));
                digits.SetReel(j, f + distance * OutBack(p, tune.Overshoot), p < 1f ? Mathf.Pow(1f - p, BlurPower) : 0f);
                if (p >= 1f && _lab.Once(KeyStopBase + j))
                {
                    Vector2 cell = timer + digits.CellPosition(j) + new Vector2(0f, -digits.CellHeight * 0.5f + 4f * px);
                    _lab.Burst(cell, 7, 140f, 320f, StopColors, gravity: 600f, life0: 0.18f, life1: 0.32f, angle: -Mathf.PI * 0.5f, spread: 1.1f);
                    _lab.Pulse += 0.05f;
                    _lab.Zoom = Mathf.Max(_lab.Zoom, 0.025f);
                }
            }
        }

        if (a >= end && _lab.Once(KeyDone))
        {
            _lab.Impact(1f);
            _lab.Ring(timer, 30f, 130f, _lab.Settings.GoldColor, 0.46f);
            _lab.Burst(timer, 20, 240f, 600f, StopColors, drag: 0.07f);
        }

        // 칩: 오른쪽에서 미끄러져 들어와 → 잠깐 떨다가 → 타이머로 빨려 들어간다.
        if (a >= 0f && a < ChipSuckEnd)
        {
            TimerBonusLab.SetText(_chip, _lab.BonusText);
            float baseY = timer.y - ChipBelowPx * px;
            if (a < ChipInSeconds)
            {
                float e = OutCubic(a / ChipInSeconds);
                TimerBonusLab.Place(_chip, new Vector2(timer.x + ChipEnterPx * px * (1f - e), baseY), new Vector2(1f + 0.4f * (1f - e), 1f), e);
            }
            else if (a < ChipHoldEnd)
                TimerBonusLab.Place(_chip, new Vector2(timer.x + Mathf.Sin(a * 250f) * 1.5f * px, baseY), Vector2.one, 1f);
            else
            {
                float e = InCubic((a - ChipHoldEnd) / (ChipSuckEnd - ChipHoldEnd));
                TimerBonusLab.Place(_chip, new Vector2(timer.x, Mathf.Lerp(baseY, timer.y, e)), new Vector2(1f - 0.7f * e, 1f - 0.9f * e), 1f - 0.8f * e);
            }
            if (a >= SuckBurstAt && _lab.Once(KeySuck))
                _lab.Burst(timer + new Vector2(0f, -20f * px), 10, 120f, 300f, _goldOnly, life0: 0.16f, life1: 0.28f, angle: Mathf.PI * 0.5f, spread: 1.2f);
        }
        else TimerBonusLab.Hide(_chip);

        float sc = a >= end ? 1f + 0.18f * Spring(a - end, 20f, 6f) : 1f;
        _lab.TimerTransform(0f, 0f, sc, sc, 0f);
        _lab.SetGold(a >= start ? (a < end ? 1f : 1f - Mathf.Clamp01((a - end - 0.15f) / 0.55f)) : 0f);
        _lab.SetFlash(a >= end ? 1f - Mathf.Clamp01((a - end) / 0.14f) : 0f);
    }
}
