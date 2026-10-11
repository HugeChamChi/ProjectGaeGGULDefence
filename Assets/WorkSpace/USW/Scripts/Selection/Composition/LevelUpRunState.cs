using System;
using System.Collections.Generic;

/// <summary>씬의 단일 카드 런 기록. 외부 지급/연출/전투 실행과 변경 알림은 조정자가 담당한다.</summary>
public sealed class LevelUpRunState
{
    private float _baseCritChance;
    private readonly Dictionary<int, SelectionCardSnapshot> _prepared = new();
    private readonly List<SelectionCardSnapshot> _active = new();
    private readonly List<SelectionCardSnapshot> _history = new();
    private readonly List<SelectionResultRecord> _results = new();
    private readonly HashSet<int> _everAcquired = new();
    private readonly HashSet<int> _pendingLegendSources = new();
    private bool _started;
    /// <summary>현재 런 식별자.</summary>
    public long RunId { get; private set; }
    /// <summary>활성 카드의 읽기 전용 획득 순서.</summary>
    public IReadOnlyList<SelectionCardSnapshot> Active { get; }
    /// <summary>제거 여부와 무관한 획득 이력.</summary>
    public IReadOnlyList<SelectionCardSnapshot> History { get; }
    /// <summary>획득 당시 확정된 결과 표시 기록.</summary>
    public IReadOnlyList<SelectionResultRecord> Results { get; }
    /// <summary>현재 활성 카드만으로 계산한 불변 효과 값.</summary>
    public SelectionEffectSnapshot Current { get; private set; }
    /// <summary>게임 설정의 기본 치명 확률을 받는다.</summary>
    public LevelUpRunState(float baseCritChance = 0f)
    {
        _baseCritChance = baseCritChance;
        Active = _active.AsReadOnly();
        History = _history.AsReadOnly();
        Results = _results.AsReadOnly();
        Current = SelectionEffectProjector.Project(_active, _baseCritChance);
    }
    /// <summary>첫 런 시작 전에 씬의 전투 설정을 연결한다. 진행 중인 런의 기본값은 변경하지 않는다.</summary>
    public void ConfigureBaseCritChance(float baseCritChance)
    {
        if (_started) throw new InvalidOperationException("Cannot change combat settings after BeginRun.");
        var initial = SelectionEffectProjector.Project(_active, baseCritChance);
        _baseCritChance = baseCritChance;
        Current = initial;
    }
    /// <summary>새 런에서만 기록을 비운다. 같은 런의 중복 Init은 보장/프리뷰를 지우지 않는다.</summary>
    public bool BeginRun(long runId)
    {
        if (_started && RunId == runId) return false;
        RunId = runId; _started = true;
        _prepared.Clear(); _active.Clear(); _history.Clear(); _results.Clear(); _everAcquired.Clear(); _pendingLegendSources.Clear();
        Current = SelectionEffectProjector.Project(_active, _baseCritChance);
        return true;
    }
    /// <summary>처음 본 카드의 정의/랜덤 값만 확정한다. 난수 API는 [min,maxExclusive) 계약이다.</summary>
    public SelectionCardSnapshot Prepare(LevelUpData card, Func<int, int, int> nextInt)
    {
        RequireRun();
        if (card == null) throw new ArgumentNullException(nameof(card));
        if (_prepared.TryGetValue(card.chooseId, out var prepared)) return prepared;
        var snapshot = new SelectionCardSnapshot(card, nextInt);
        _prepared.Add(card.chooseId, snapshot);
        return snapshot;
    }
    /// <summary>제거 후에도 이번 런에서 획득했던 카드는 true다.</summary>
    public bool HasEverAcquired(int cardId) => _everAcquired.Contains(cardId);
    /// <summary>준비된 카드를 한 번만 획득한다. 계산 오류는 어떤 획득 기록도 남기지 않는다.</summary>
    public bool TryAcquire(int cardId, string resultDescription = null)
    {
        RequireRun();
        if (_everAcquired.Contains(cardId) || !_prepared.TryGetValue(cardId, out var card)) return false;
        var candidate = new List<SelectionCardSnapshot>(_active) { card };
        var projected = SelectionEffectProjector.Project(candidate, _baseCritChance);
        _results.Add(new SelectionResultRecord(card, resultDescription));
        _active.Add(card); _history.Add(card); _everAcquired.Add(cardId); Current = projected;
        if (card.HasCommand<GuaranteeLegendCommandDefinition>()) _pendingLegendSources.Add(cardId);
        return true;
    }
    /// <summary>지속 기여만 제거한다. 이력/프리뷰/지급 결과는 되돌리지 않는다.</summary>
    public bool Remove(int cardId)
    {
        RequireRun();
        int index = _active.FindIndex(card => card.CardId == cardId);
        if (index < 0) return false;
        var candidate = new List<SelectionCardSnapshot>(_active); candidate.RemoveAt(index);
        var projected = SelectionEffectProjector.Project(candidate, _baseCritChance);
        _active.RemoveAt(index); _pendingLegendSources.Remove(cardId); Current = projected;
        return true;
    }
    /// <summary>추첨 조정자가 기존 소비 시점에서 호출한다. 재계산으로 보장이 되살아나지 않는다.</summary>
    public bool ConsumeLegendGuarantee()
    {
        RequireRun();
        bool guaranteed = _pendingLegendSources.Count > 0;
        _pendingLegendSources.Clear();
        return guaranteed;
    }
    private void RequireRun() { if (!_started) throw new InvalidOperationException("BeginRun must precede selection operations."); }
}
