using System;

/// <summary>생성 주기의 초과 시간을 보존한다. 공간 부족으로 완충 상태가 이어지면 한 번만 저장한다.</summary>
public sealed class TotemSpawnCharge
{
    private double _elapsed;
    private double _interval;
    /// <summary>지정 주기의 충전 비율.</summary>
    public float Progress(double interval) => interval > 0 ? (float)Math.Min(1, _elapsed / interval) : 0f;
    /// <summary>충전 완료 여부.</summary>
    public bool IsReady(double interval) => interval > 0 && _elapsed >= interval;
    /// <summary>활성 전투 시간을 더한다. 이전 프레임에도 완충이었다면 공간 대기 시간은 버린다.</summary>
    public void Advance(double deltaTime, double interval)
    {
        if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0 ||
            double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0)
            throw new ArgumentOutOfRangeException(nameof(interval));
        _interval = interval;
        _elapsed = _elapsed >= interval ? interval : _elapsed + deltaTime;
    }
    /// <summary>소환 성공 시에만 충전을 소비한다.</summary>
    public void Consume() => _elapsed = Math.Max(0, _elapsed - _interval);
}
