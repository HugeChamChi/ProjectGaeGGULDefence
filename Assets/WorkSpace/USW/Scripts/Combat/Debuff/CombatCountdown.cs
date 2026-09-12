using System;

/// <summary>제한시간까지 진행한 효과를 먼저 처리한 뒤 시간초과를 판정하는 순수 전투 시계.</summary>
public sealed class CombatCountdown
{
    /// <summary>제한시간으로 제한된 누적 전투 시간. 시간초과 이벤트보다 먼저 발행한다.</summary>
    public event Action<double> OnAdvanced;
    /// <summary>도래 효과 처리 후에도 실행 중인 경우 시간초과.</summary>
    public event Action OnTimeUp;
    /// <summary>남은 시간.</summary>
    public double Remaining { get; private set; }
    /// <summary>이번 카운트다운에서 실제로 진행한 시간.</summary>
    public double Elapsed { get; private set; }
    /// <summary>일시정지/사망/종료 상태.</summary>
    public bool IsRunning { get; private set; }
    /// <summary>새 전투 시간 초기화.</summary>
    public void Start(double seconds)
    {
        Validate(seconds);
        if (seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        Remaining = seconds; Elapsed = 0; IsRunning = true;
    }
    /// <summary>남은 시간을 넘겨 진행하지 않는다. 콜백에서 Stop하면 막판 처치가 우선한다.</summary>
    public void Advance(double delta)
    {
        Validate(delta);
        if (!IsRunning || delta == 0) return;
        double step = Math.Min(delta, Remaining);
        Remaining -= step; Elapsed += step;
        OnAdvanced?.Invoke(Elapsed);
        if (IsRunning && Remaining <= 0)
        { IsRunning = false; OnTimeUp?.Invoke(); }
    }
    /// <summary>사망/정지 시 호출한다.</summary>
    public void Stop() => IsRunning = false;
    /// <summary>남은 시간이 있으면 재개한다.</summary>
    public void Resume() { if (Remaining > 0) IsRunning = true; }
    /// <summary>추가 시간은 기존 효과의 경과 시간을 되돌리지 않는다.</summary>
    public void AddTime(double seconds)
    {
        Validate(seconds);
        if (double.IsInfinity(Remaining + seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        Remaining += seconds;
    }
    private static void Validate(double value)
    { if (value < 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); }
}
