using System;
using VContainer.Unity;

/// <summary>씬 전투 타이머를 집계에 연결한다. 창이 닫혀 있어도 기록하고 새 런에서 초기화한다.</summary>
public sealed class DamageContributionBinding : IInitializable, IDisposable
{
    private readonly DamageContributionLedger _ledger;
    private readonly TimerController _timer;
    private readonly GameManager _game;
    private double _previousTime;

    /// <summary>동일 씬의 권위 있는 전투 시계와 런 시작 신호를 받는다.</summary>
    public DamageContributionBinding(DamageContributionLedger ledger, TimerController timer, GameManager game)
    { _ledger = ledger; _timer = timer; _game = game; }

    /// <summary>보스가 생성되기 전에 시계 이벤트를 구독한다.</summary>
    public void Initialize()
    {
        _previousTime = _timer.ElapsedCombatTime;
        _timer.OnCombatTimeAdvanced += Advance;
        _game.OnGameStart += Reset;
    }
    private void Reset() { _ledger.Reset(); _previousTime = 0; }
    private void Advance(double elapsed)
    {
        _ledger.Advance(Math.Max(0, elapsed - _previousTime));
        _previousTime = elapsed;
    }
    /// <summary>씬 수명 종료 때 이벤트를 정리한다.</summary>
    public void Dispose()
    { _timer.OnCombatTimeAdvanced -= Advance; _game.OnGameStart -= Reset; }
}
