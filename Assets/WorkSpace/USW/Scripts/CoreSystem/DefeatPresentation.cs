using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Owns only the brief defeat speed request. Result pause is applied before this owner releases.</summary>
public sealed class DefeatPresentation
{
    private readonly TimeScaleService _time;
    /// <summary>Uses the scene's overlapping-owner time service.</summary>
    public DefeatPresentation(TimeScaleService time) => _time = time;
    /// <summary>Waits in real time even if another owner paused gameplay; cancellation never opens a stale result.</summary>
    public async UniTask PlayAsync(GameConfig config, Action showResult, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _time.Request(this, config.DefeatTimeScale);
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(config.DefeatDuration), DelayType.Realtime, cancellationToken: token);
            token.ThrowIfCancellationRequested();
            showResult();
        }
        finally { _time.Release(this); }
    }
}
