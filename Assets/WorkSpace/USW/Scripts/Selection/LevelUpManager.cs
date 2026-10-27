using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;

/// <summary>씬의 선택 진입점. 추첨·상태 계산·지급을 조정하며 전투 기능 자체를 실행하지 않는다.</summary>
public class LevelUpManager : MonoBehaviour
{
    [Inject] private GridManager _gridManager;
    [Inject] private TotemBuffManager _totemBuffManager;
    [Inject] private CurrencyManager _currencyManager;

    [Inject] private UnitFactory _unitFactoryManager;
    [Inject] private UnitSpawner _spawnerManager;
    [Inject] private ChieftainSelection _chieftainSelection;
    [Inject] private UpgradeManager _upgradeManager;
    [Inject] private GameManager _gameManager;

    [Inject] private EndlessRunService _runPenalties;
    [Inject] private LevelUpDrawer _drawer;
    [Inject] private LevelUpRunState _runState;
    [Inject] private SelectionEffectReader _reader;
    [Inject] private SelectionCommandExecutor _commands;
    [Inject] private SelectionDescriptionFormatter _formatter;
    [SerializeField, HideInInspector] private LevelUpData[] levelUpPool;
    [SerializeField] private GameConfig _gameConfig;
    private bool _runtimeInitialized;
    private bool _processing;
    private readonly Queue<Action> _pending = new();
    private ILevelUpCatalog _catalog;
    private bool _poolInitialized;
    private LevelUpPoolData _selectedPool;
    private LevelUpData[] _effectivePool = Array.Empty<LevelUpData>();
    private LevelUpRunState State { get { EnsureRuntime(); return _runState; } }
    /// <summary>획득 이력은 제거 후에도 유지한다.</summary>
    public IEnumerable<int> ChosenIds { get { foreach (var card in State.History) yield return card.CardId; } }
    /// <summary>확정 풀의 배열 사본.</summary>
    public LevelUpData[] LevelUpPool => (LevelUpData[])_effectivePool.Clone();
    /// <summary>지속 효과 변경 후의 현재 불변 계산 결과.</summary>
    public SelectionEffectSnapshot CurrentEffects => State.Current;
    /// <summary>일회 토템 선택 요청.</summary>
    public event Action<Action> OnTotemSelectionRequested;
    [Inject] private CombatSettings _combatSettings;
    /// <summary>씬 구성 시 직렬화된 원본에서 한 번 고정하는 전투 설정.</summary>
    public CombatSettings CombatConfiguration => _combatSettings ??= new CombatSettings(_gameConfig);
    /// <summary>디버그/독립 검사용 읽기 전용 조회. 제품 소비자는 기능별 인터페이스를 주입받는다.</summary>
    public SelectionEffectReader Effects { get { EnsureRuntime(); return _reader; } }
    private void EnsureRuntime()
    {
        if (_runtimeInitialized) return;
        _runState ??= new LevelUpRunState();
        _runState.ConfigureBaseCritChance(CombatConfiguration.BaseCritChance);
        _runState.BeginRun(GetInstanceID());
        _reader ??= new SelectionEffectReader(_runState);
        _drawer ??= new LevelUpDrawer(new UnitySelectionRandom());
        _formatter ??= new SelectionDescriptionFormatter();
        _runtimeInitialized = true;
    }
    /// <summary>선택된 족장의 풀을 런 시작에 한 번 확정한다. 런 상태는 씬 수명을 따른다.</summary>
    public void Init()
    {
        EnsureRuntime();
        if (_poolInitialized) return;
        _poolInitialized = true;
        var pool = _chieftainSelection?.GetSelectedLevelUpPool();
        _selectedPool = pool;
        try
        {
            _catalog = new LevelUpCatalog(new[] { pool });
            var cards = new List<LevelUpData>();
            foreach (int id in _catalog.GetCardIds(pool.PoolId))
                if (_catalog.TryGetCard(id, out var card)) cards.Add(card);
            _effectivePool = cards.ToArray();
        }
        catch (System.ArgumentException exception)
        {
            Debug.LogError($"[LevelUp] 족장 풀 설정 오류: {exception.Message}", this);
            _effectivePool = System.Array.Empty<LevelUpData>();
        }
    }

    /// <summary>확정 프리뷰로 설명을 만들며 반복 조회는 재추첨하지 않는다.</summary>
    public string GetChoiceDescription(LevelUpData card)
        => card == null ? string.Empty : GetFormatter().Format(State.Prepare(card, UnityEngine.Random.Range));
    /// <summary>설명 UI와 실제 획득이 같은 런 캐시의 확정 값을 사용한다.</summary>
    public SelectionCardSnapshot PrepareChoice(LevelUpData card) => State.Prepare(card, UnityEngine.Random.Range);
    /// <summary>상세/요약 UI가 실제 효과와 같은 런 프리뷰의 확정 수치를 사용한다.</summary>
    public DescriptionSnapshot GetChoiceDescriptionSnapshot(LevelUpData card)
        => card == null ? new DescriptionSnapshot(null, null)
            : GetFormatter().Capture(State.Prepare(card, UnityEngine.Random.Range), card.simpleDescription);
    private SelectionDescriptionFormatter GetFormatter() { EnsureRuntime(); return _formatter; }
    /// <summary>선택 연출 전에 재추첨 여부를 조회한다.</summary>
    public bool RequestsReroll(LevelUpData card)
        => card != null && State.Prepare(card, UnityEngine.Random.Range).HasCommand<RerollChoicesCommandDefinition>();
    /// <summary>획득 시 확정한 정의와 표시값으로 결과 화면 기록을 제공한다.</summary>
    public ResultBuildChoice[] GetResultChoices()
    {
        var state = State;
        var result = new ResultBuildChoice[state.Results.Count];
        for (int i=0;i<result.Length;i++)
        {
            var card=state.Results[i];
            result[i]=new ResultBuildChoice { Icon=card.Icon, Name=card.Name, Description=card.Description };
        }
        return result;
    }
    /// <summary>현재 씬 조건으로 추첨하고 유효한 레전더리 목록을 얻은 뒤에만 보장을 소비한다.</summary>
    public List<LevelUpData> GetRandomChoices(int count=3)
    {
        if (count<=0 || _gameManager?.IsFinished==true) return new List<LevelUpData>();
        count=_runPenalties?.GetChoiceCount(count) ?? count;
        if (!_poolInitialized) Init();
        bool forceLegend=State.HasLegendGuarantee;
        var tribes=GetPresentTribes();
        var choices=_drawer.Draw(_effectivePool,State.HasEverAcquired,tribes,_selectedPool,count,forceLegend);
        if (forceLegend && choices.Count > 0) State.ConsumeLegendGuarantee();
        else if (forceLegend) choices=_drawer.Draw(_effectivePool,State.HasEverAcquired,tribes,_selectedPool,count,false);
        return choices;
    }
    private HashSet<UnitTribe> GetPresentTribes()
    {
        var tribes = new HashSet<UnitTribe>();
        if (_gridManager == null) return tribes;
        foreach (var cell in _gridManager.GetOccupiedCells())
        {
            if (cell.OccupyingUnit?.unitData != null)
                tribes.Add(cell.OccupyingUnit.unitData.unitTribe);
        }
        return tribes;
    }


    /// <summary>UI 선택은 현재 풀의 실제 참조만 수락하고 확정 결과를 반환한다.</summary>
    public SelectionApplyResult TryApplyChoice(LevelUpData data)
    {
        if (!_poolInitialized) Init();
        if (data == null || Array.IndexOf(_effectivePool,data)<0) return SelectionApplyResult.Rejected;
        return RequestApply(data);
    }
    /// <summary>검증/디버그를 포함한 기존 직접 효과 진입점. 중복 획득과 종료 후 호출은 거절한다.</summary>
    public void ApplyEffect(LevelUpData data) => RequestApply(data);
    private SelectionApplyResult RequestApply(LevelUpData data)
    {
        if (data == null || _gameManager?.IsFinished==true || State.HasEverAcquired(data.chooseId)) return SelectionApplyResult.Rejected;
        if (_processing) { _pending.Enqueue(()=>ApplyNow(data)); return SelectionApplyResult.Queued; }
        _processing=true;
        try { return ApplyNow(data); }
        finally { DrainPending(); }
    }
    private SelectionApplyResult ApplyNow(LevelUpData data)
    {
        if (data == null || _gameManager?.IsFinished==true || State.HasEverAcquired(data.chooseId)) return SelectionApplyResult.Rejected;
        SelectionCardSnapshot card;
        try
        {
            card=State.Prepare(data,UnityEngine.Random.Range);
            string description=_formatter.Format(card);
            if (!State.TryAcquire(card.CardId,description)) return SelectionApplyResult.Rejected;
        }
        catch (ArgumentException error) { Debug.LogError("[LevelUp] 선택 거절: "+error.Message,this); return SelectionApplyResult.Rejected; }
        CommitPersistentEffects();
        NotifyChanges();
        var executor=_commands ?? new SelectionCommandExecutor(_currencyManager,_upgradeManager,_gridManager,_unitFactoryManager,_spawnerManager,_gameManager,new UnitySelectionRandom());
        executor.Execute(card,RequestTotem,this.GetCancellationTokenOnDestroy());
        return card.HasCommand<RerollChoicesCommandDefinition>() ? SelectionApplyResult.Reroll : SelectionApplyResult.Applied;
    }
    /// <summary>활성 기여만 제거한다. 이력/프리뷰/즉시 지급은 유지한다.</summary>
    public void RemoveEffect(LevelUpData data)
    {
        if (data == null || _gameManager?.IsFinished==true) return;
        if (_processing) { _pending.Enqueue(()=>RemoveNow(data.chooseId)); return; }
        _processing=true;
        try { RemoveNow(data.chooseId); }
        finally { DrainPending(); }
    }
    private void RemoveNow(int id)
    {
        if (_gameManager?.IsFinished==true || !State.Remove(id)) return;
        CommitPersistentEffects(); NotifyChanges();
    }
    private void CommitPersistentEffects()
    {
        _upgradeManager?.ReplaceSelectionDiscounts(State.Current.UpgradeDiscounts);
        _totemBuffManager?.ReplaceSelectionEffects(State.Current);
    }
    private void NotifyChanges()
    {
        SafeNotify(()=>_totemBuffManager?.NotifySelectionEffectsChanged());
        SafeNotify(()=>_upgradeManager?.NotifySelectionDiscountsChanged());
        _reader.Publish();
    }
    private static void SafeNotify(Action notify) { try { notify(); } catch(Exception error) { Debug.LogException(error); } }
    private void RequestTotem()
    {
        if (OnTotemSelectionRequested==null) return;
        foreach(Action<Action> handler in OnTotemSelectionRequested.GetInvocationList()) SafeNotify(()=>handler(null));
    }
    private void DrainPending()
    {
        try { while(_pending.Count>0) { var action=_pending.Dequeue(); SafeNotify(action); } }
        finally { _processing=false; }
    }
}
