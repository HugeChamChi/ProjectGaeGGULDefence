using System;

/// <summary>생성 대기 게이지. 공간 부족 시 완충을 유지하며 한 번 소비하면 처음부터 충전한다.</summary>
public sealed class TotemSpawnCharge
{
    private double _elapsed;
    /// <summary>지정 주기의 충전 비율.</summary>
    public float Progress(double interval) => interval > 0 ? (float)Math.Min(1, _elapsed / interval) : 0f;
    /// <summary>충전 완료 여부.</summary>
    public bool IsReady(double interval) => interval > 0 && _elapsed >= interval;
    /// <summary>활성 전투 시간만 전달한다. 초과 시간은 소환 부채로 누적하지 않는다.</summary>
    public void Advance(double deltaTime, double interval)
    {
        if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0 ||
            double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval));
        _elapsed = Math.Min(interval, _elapsed + deltaTime);
    }
    /// <summary>소환 성공 시에만 충전을 소비한다.</summary>
    public void Consume() => _elapsed = 0;
}
