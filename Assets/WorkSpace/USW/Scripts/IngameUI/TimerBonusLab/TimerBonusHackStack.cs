using UnityEngine;

/// <summary>
/// 시안 G~J — 타이머 해킹(E)을 여러 캐릭터가 연달아 발동할 때의 처리 비교 (사용자 요청 2026-10-09).
/// 같은 발동 시점(HackTuning.StackTriggerTimes)을 네 가지 규칙으로 보여 준다:
///   G 재시작: 발동마다 처음부터 다시 (규칙 없이 붙였을 때 — 비교 기준)
///   H 대기열: 앞 해킹이 끝나야 다음 해킹 (표시가 실제보다 늦어짐)
///   I 합산: 진행 중 발동은 지금 해킹에 보너스를 더하고 고정을 조금 늦춤, 끝난 뒤 발동은 새 해킹
///   J 합산+미니: 고정 시작 전 발동만 합산. 고정 시작 후·끝난 뒤 발동은 끝나자마자 짧은 버전으로 이어 붙이고
///                결과 글자는 덮어쓰지 않고 누적("+45 x3" → "+60 x4"). 사용자 요청으로 "끝날 때쯤 들어오면" 보강 (2026-10-09).
/// 진행 막대는 합쳐져도 뒤로 가지 않는다(전 시안 공통). I는 비교용으로 이전 규칙(고정 끝 전까지 합산) 유지.
/// 시간 보너스 자체는 발동 순간 붙는다고 가정하고, 표시만 규칙에 따라 따라간다.
/// 네 시안이 한 렌더러(TimerBonusHack)를 같이 쓴다. 발동 순간마다 해당 드론에서 하늘색 신호가 타이머로 날아간다.
/// 인게임(J만 사용): 발동 목록은 실제 디시그망 스킬이 들어올 때마다 늘어나고, 신호는 그 디시그망 위치에서 발동 순간 출발한다.
/// </summary>
public sealed class TimerBonusHackStack : ITimerBonusConcept
{
    /// <summary>겹침 처리 규칙.</summary>
    public enum Mode { Restart, Queue, Merge, MergeMini }

    private const int MaxRuns = 16;
    private const int KeySignalBase = 9000;
    private const int KeyMergeBase = 9100;
    private const int SignalDots = 6;
    private const float MergeRingAlpha = 0.3f;

    private readonly TimerBonusHack _renderer;
    private readonly Mode _mode;
    private readonly TimerBonusHack.Run[] _runs = new TimerBonusHack.Run[MaxRuns];
    private TimerBonusLab _lab;
    private Color[] _signalColors;

    /// <summary>처치(첫 발동) 기준 초 — 지금까지 발동한 해킹이 모두 사라지는 순간. 인게임 연출 종료 판정에 쓴다.</summary>
    internal float VisibleEnd { get; private set; }

    /// <param name="renderer">시안끼리 같이 쓰는 해킹 렌더러 (Setup은 처음 한 번만 실제로 만든다)</param>
    public TimerBonusHackStack(TimerBonusHack renderer, Mode mode)
    {
        _renderer = renderer;
        _mode = mode;
    }

    /// <inheritdoc />
    public string Name => _mode switch
    {
        Mode.Restart => "G 재시작",
        Mode.Queue => "H 대기열",
        Mode.Merge => "I 합산",
        _ => "J 합산+미니",
    };

    /// <inheritdoc />
    public void Setup(TimerBonusLab lab)
    {
        _lab = lab;
        _renderer.Setup(lab);
        _signalColors = new[] { lab.Settings.CyanColor, Color.white };
    }

    /// <inheritdoc />
    public void SetVisible(bool visible) => _renderer.Show(this, visible);

    /// <inheritdoc />
    public void Render(float a, float step)
    {
        var tune = _lab.Settings.Hack;
        var triggers = _lab.StackTriggerTimes;
        int count = triggers != null ? Mathf.Min(triggers.Length, MaxRuns) : 0;
        EmitSignals(a, triggers, count, tune.SignalSeconds);

        int runCount = BuildRuns(a, triggers, count, tune);
        // 지금 그릴 해킹 = 이미 시작한 것 중 마지막. 아직 하나도 없으면 첫 해킹의 시작 전 모습(평소 타이머).
        int current = 0;
        for (int i = 0; i < runCount; i++)
            if (_runs[i].Start <= a) current = i;
        if (runCount == 0) _runs[0] = new TimerBonusHack.Run { Bonus = _lab.Bonus, Stacks = 1 };
        VisibleEnd = runCount > 0 ? _runs[runCount - 1].Start + _renderer.VisibleUntilOf(_runs[runCount - 1]) : 0f;
        _renderer.RenderRun(a, _runs[current]);
    }

    // 지금(a)까지 발동한 것만으로 해킹 목록을 만든다 (미래 발동은 모름). 매 프레임 처음부터 다시 계산해서 같은 a에는 같은 화면.
    private int BuildRuns(float a, float[] triggers, int count, TimerBonusLabSettings.HackTuning tune)
    {
        int n = 0;
        double applied = 0d;
        for (int i = 0; i < count; i++)
        {
            float t = triggers[i];
            if (t > a) break;
            float bonus = _lab.StackTriggerBonus(i);
            switch (_mode)
            {
                case Mode.Restart:
                    _runs[n++] = new TimerBonusHack.Run { Index = n, Start = t, BaseAdded = applied, Bonus = bonus, Stacks = 1 };
                    break;
                case Mode.Queue:
                {
                    float start = n == 0 ? t : Mathf.Max(t, _runs[n - 1].Start + _renderer.FinishOf(_runs[n - 1]));
                    _runs[n++] = new TimerBonusHack.Run { Index = n, Start = start, BaseAdded = applied, Bonus = bonus, Stacks = 1 };
                    break;
                }
                default:
                {
                    bool late = _mode == Mode.MergeMini;
                    // I: 고정이 끝나기 전이면 합산 / J: 고정이 시작되기 전이어야 합산 (고정된 자리를 다시 풀지 않게)
                    float cutoff = n > 0 ? _runs[n - 1].Start + (late ? _renderer.LockStartOf(_runs[n - 1]) : _renderer.EndOf(_runs[n - 1])) : 0f;
                    if (n > 0 && !_runs[n - 1].Mini && t < cutoff)
                    {
                        // 진행 중 → 합치고, 합쳐진 순간 이후로 고정이 MergeExtend만큼은 남게 늦춘다.
                        ref var run = ref _runs[n - 1];
                        float local = t - run.Start;
                        run.ProgressFrom = _renderer.RawProgress(run, local);
                        run.ProgressFromTime = local;
                        run.Bonus += bonus;
                        run.Stacks++;
                        float need = local + tune.MergeExtendSeconds - (tune.InfectSeconds + tune.HackSeconds);
                        run.LockDelay = Mathf.Max(run.LockDelay + tune.MergeExtendSeconds, need);
                        if (_lab.Once(KeyMergeBase + i)) _lab.Ring(_lab.TimerPosition, 20f, 70f, WithAlpha(_lab.Settings.CyanColor, MergeRingAlpha), 0.25f);
                    }
                    else if (late && n > 0)
                    {
                        // J: 앞 결과가 아직 보이면 끝나자마자(또는 앞 미니 직후) 이어 붙이고 결과 숫자를 누적, 아니면 단독 미니.
                        ref var prev = ref _runs[n - 1];
                        float prevEnd = prev.Start + _renderer.EndOf(prev);
                        bool chain = t < prevEnd + tune.ChainWindowSeconds;
                        float start = chain ? Mathf.Max(t, prev.Mini ? prev.Start + tune.ChainGapSeconds : prevEnd) : t;
                        float shown = chain ? (prev.DisplayStacks > 0 ? prev.DisplayBonus : prev.Bonus) : 0f;
                        int shownStacks = chain ? (prev.DisplayStacks > 0 ? prev.DisplayStacks : prev.Stacks) : 0;
                        _runs[n++] = new TimerBonusHack.Run
                        {
                            Index = n, Start = start, BaseAdded = applied, Bonus = bonus, Stacks = 1, Mini = true, Chain = chain,
                            DisplayBonus = shown + bonus, DisplayStacks = shownStacks + 1,
                        };
                    }
                    else _runs[n++] = new TimerBonusHack.Run { Index = n, Start = t, BaseAdded = applied, Bonus = bonus, Stacks = 1 };
                    break;
                }
            }
            applied += bonus;
        }
        // 합산 모드에서 BaseAdded는 앞 해킹들이 붙인 합계 — 합쳐진 보너스는 그 해킹의 Bonus에 들어 있다.
        if (_mode == Mode.Merge || _mode == Mode.MergeMini)
        {
            double sum = 0d;
            for (int i = 0; i < n; i++) { _runs[i].BaseAdded = sum; sum += _runs[i].Bonus; }
        }
        return n;
    }

    // 발동 순간에 맞춰 도착하도록, 그 드론에서 SignalSeconds 먼저 하늘색 점들을 쏜다.
    private void EmitSignals(float a, float[] triggers, int count, float lead)
    {
        var slots = _lab.Settings.DroneSlots;
        bool live = _lab.IsRuntime;
        if (!live && (slots == null || slots.Length == 0)) return;
        for (int i = 0; i < count; i++)
        {
            // 실험실은 발동 시점을 미리 알아 도착에 맞춰 먼저 쏘고, 인게임은 발동 순간 그 유닛에서 출발한다.
            if (a < triggers[i] - (live ? 0f : lead) || !_lab.Once(KeySignalBase + i)) continue;
            Vector2 from;
            if (live) { if (!_lab.TryGetTriggerOrigin(i, out from)) continue; }
            else from = _lab.BossCenter + slots[i % slots.Length] * _lab.Px;
            for (int d = 0; d < SignalDots; d++)
            {
                _lab.Emit(new TimerBonusParticles.Particle
                {
                    Kind = TimerBonusParticles.Kind.Home,
                    Position = from,
                    Velocity = Random.insideUnitCircle * 120f * _lab.Px,
                    Size = TimerBonusLab.Rnd(2f, 3.5f) * _lab.Px,
                    MaxLife = lead + 0.1f,
                    HomeDelay = 0f,
                    HomeDuration = Mathf.Max(0.05f, lead - d * 0.012f),
                    Target = _lab.TimerPosition,
                    Curve = TimerBonusLab.Rnd(-60f, 60f) * _lab.Px,
                    Color = _signalColors[d % _signalColors.Length],
                });
            }
            _lab.Ring(from, 10f, 45f, _lab.Settings.CyanColor, 0.25f);
        }
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
