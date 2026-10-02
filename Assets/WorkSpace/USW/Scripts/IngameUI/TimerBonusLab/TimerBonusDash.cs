using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static TimerBonusEase;

/// <summary>
/// 시안 C 질주 — 속도선이 화면을 가르고, 숫자가 반대로 살짝 젖혔다가 앞으로 쏠리며 RGB 잔상과 함께 순식간에 올라간다.
/// 타이머 아래 게이지의 금색 구간이 늘어난 시간을 보여 준다.
/// </summary>
public sealed class TimerBonusDash : ITimerBonusConcept
{
    private const int KeyGo = 100;
    private const int KeySnap = 101;
    private const float BarWidthPx = 184f;
    private const float BarHeightPx = 8f;
    private const float BarGapPx = 12f;
    private const float LabelHoldSeconds = 0.5f;
    private const float LabelFadeSeconds = 0.3f;
    private const float StreakTailSeconds = 0.12f;
    private const float EdgeSparksPerSecond = 60f;
    private const float NearStreakChance = 0.6f;
    private const float CyanStreakChance = 0.3f;

    private TimerBonusLab _lab;
    private Image _barBack;
    private RectTransform _fill;
    private RectTransform _add;
    private Image _addImage;
    private TextMeshProUGUI _label;
    private float _streakAcc;
    private float _sparkAcc;
    private Color[] _edgeColors;

    /// <inheritdoc />
    public string Name => "C 질주";

    /// <inheritdoc />
    public void Setup(TimerBonusLab lab)
    {
        _lab = lab;
        var s = lab.Settings;
        float px = lab.Px;
        _barBack = TimerBonusLab.CreateImage("DashBar", lab.FxLayer, new Color(1f, 1f, 1f, 0.12f));
        _barBack.rectTransform.sizeDelta = new Vector2(BarWidthPx, BarHeightPx) * px;
        _barBack.rectTransform.anchoredPosition = new Vector2(lab.TimerPosition.x, BarY);
        _fill = NewBarPart("Fill", s.TimerColor).rectTransform;
        _addImage = NewBarPart("Add", s.GoldColor);
        _add = _addImage.rectTransform;
        _label = lab.CreateText("DashLabel", 20f, s.GoldColor);
        _edgeColors = new[] { s.GoldColor };
    }

    /// <inheritdoc />
    public void SetVisible(bool visible)
    {
        _barBack.gameObject.SetActive(visible);
        _label.gameObject.SetActive(visible);
    }

    /// <inheritdoc />
    public void Render(float a, float step)
    {
        var tune = _lab.Settings.Dash;
        float px = _lab.Px;
        float d0 = tune.WindUpSeconds, cu = tune.CountUpSeconds;
        Vector2 timer = _lab.TimerPosition;
        if (a < 0f) { _streakAcc = 0f; _sparkAcc = 0f; }

        float now = (float)_lab.Now;
        float applied = a >= d0 ? _lab.Bonus * OutCubic(Mathf.Clamp01((a - d0) / cu)) : 0f;
        _lab.SetTimerValue(now + applied);

        // 게이지: 흰색 = 원래 남은 시간, 금색 = 이번에 붙은 시간
        float width = BarWidthPx * px;
        float w0 = Mathf.Clamp01(now / tune.BarMaxSeconds);
        float w1 = Mathf.Clamp01((now + applied) / tune.BarMaxSeconds);
        _fill.sizeDelta = new Vector2(w0 * width, 0f);
        _add.anchoredPosition = new Vector2(w0 * width, 0f);
        _add.sizeDelta = new Vector2((w1 - w0) * width, 0f);
        _addImage.canvasRenderer.SetAlpha(a >= d0 ? 1f : 0f);
        float barLeft = timer.x - width * 0.5f;
        float labelAlpha = a < d0 ? 0f : 1f - Mathf.Clamp01((a - d0 - cu - LabelHoldSeconds) / LabelFadeSeconds);
        TimerBonusLab.SetText(_label, _lab.BonusText);
        TimerBonusLab.Place(_label, new Vector2(barLeft + w1 * width, BarY - (BarGapPx + 10f) * px), Vector2.one, labelAlpha);

        // 쏠림: 준비(뒤로 젖힘) → 돌진(앞으로 기울며 왼쪽으로) → 스프링으로 제자리
        float lean = tune.LeanDegrees, skew = 0f, tx = 0f, sc = 1f;
        bool dashing = a >= d0 && a < d0 + cu;
        if (a >= 0f && a < d0)
        {
            float q = a / d0;
            skew = -0.5f * lean * q;
            tx = 9f * q;
        }
        else if (dashing)
        {
            float q = (a - d0) / cu;
            skew = lean * Mathf.Lerp(1f, 0.6f, q);
            tx = Mathf.Lerp(-18f, -8f, q);
            sc = 1f + 0.06f * (1f - q);
        }
        else if (a >= d0 + cu)
        {
            float d = Spring(a - d0 - cu, 20f, 6f);
            skew = 0.6f * lean * d;
            tx = -8f * d;
            sc = 1f + 0.1f * Mathf.Max(0f, d);
        }
        _lab.TimerTransform(tx, 0f, sc * (dashing ? 1.06f : 1f), sc, skew);

        float offset = a >= d0 ? tune.RgbOffset * (1f - Mathf.Clamp01((a - d0) / (cu + 0.26f))) : 0f;
        _lab.SetGhosts(offset, offset > 0.2f ? 0.9f : 0f);
        _lab.SetGold(a >= d0 ? 1f - Mathf.Clamp01((a - d0 - cu - 0.1f) / 0.6f) : 0f);
        _lab.SetFlash(a >= d0 + cu ? 0.6f * (1f - Mathf.Clamp01((a - d0 - cu) / 0.16f)) : 0f);

        if (step > 0f && a >= 0f && a < d0 + cu + StreakTailSeconds)
        {
            float intensity = a < d0 + cu ? 1f : 1f - (a - d0 - cu) / StreakTailSeconds;
            _streakAcc += tune.StreaksPerSecond * intensity * step;
            while (_streakAcc >= 1f) { _streakAcc -= 1f; EmitStreak(timer, px); }
            if (a >= d0)
            {
                _sparkAcc += EdgeSparksPerSecond * step;
                while (_sparkAcc >= 1f)
                {
                    _sparkAcc -= 1f;
                    _lab.Emit(new TimerBonusParticles.Particle
                    {
                        Kind = TimerBonusParticles.Kind.Spark,
                        Position = new Vector2(barLeft + w1 * width, BarY),
                        Velocity = new Vector2(-TimerBonusLab.Rnd(160f, 420f), TimerBonusLab.Rnd(-90f, 90f)) * px,
                        Size = TimerBonusLab.Rnd(1.5f, 2.5f) * px,
                        MaxLife = TimerBonusLab.Rnd(0.14f, 0.24f),
                        Drag = 0.06f,
                        Color = _edgeColors[0],
                    });
                }
            }
        }

        if (a >= d0 && _lab.Once(KeyGo)) _lab.Zoom = Mathf.Max(_lab.Zoom, 0.05f);
        if (a >= d0 + cu && _lab.Once(KeySnap))
        {
            _lab.Impact(0.9f);
            _lab.Ring(timer, 30f, 115f, _lab.Settings.CyanColor, 0.36f);
        }
    }

    private float BarY => _lab.TimerPosition.y - _lab.TimerSize.y * 0.5f - (BarGapPx * 0.5f + BarHeightPx * 0.5f) * _lab.Px;

    private void EmitStreak(Vector2 timer, float px)
    {
        Vector2 half = _lab.StageSize * 0.5f;
        bool near = Random.value < NearStreakChance;
        float y = near ? timer.y + TimerBonusLab.Rnd(-60f, 50f) * px : TimerBonusLab.Rnd(-half.y + 20f * px, half.y - 10f * px);
        _lab.Emit(new TimerBonusParticles.Particle
        {
            Kind = TimerBonusParticles.Kind.Streak,
            Position = new Vector2(half.x + TimerBonusLab.Rnd(0f, 40f) * px, y),
            Velocity = new Vector2(-TimerBonusLab.Rnd(1800f, 3200f) * px, 0f),
            Length = TimerBonusLab.Rnd(40f, 150f) * px,
            Size = TimerBonusLab.Rnd(1f, 2.6f) * px,
            MaxLife = TimerBonusLab.Rnd(0.26f, 0.42f),
            Color = Random.value < CyanStreakChance ? _lab.Settings.CyanColor : Color.white,
            Alpha = near ? 0.85f : 0.35f,
        });
    }

    // 게이지 왼쪽 끝 기준으로 가로 길이만 바꾸는 막대
    private Image NewBarPart(string name, Color color)
    {
        var img = TimerBonusLab.CreateImage(name, _barBack.rectTransform, color);
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return img;
    }
}
