// ════════════════════════════════════════════════════════
// TimerController — InGameSingleton 교체
// ════════════════════════════════════════════════════════
using UnityEngine;
using System;
using VContainer;

[DefaultExecutionOrder(-100)]
public class TimerController : MonoBehaviour
{ 
    [VContainer.Inject] private FieldPauseVisuals _fieldPause;
    [VContainer.Inject] private AudioManager _audioManager;
    [VContainer.Inject] private IOverdriveEffectReader _overdrive;
    [VContainer.Inject] private LevelUpRunState _selections;
    [VContainer.Inject] private GameManager _game;
    [VContainer.Inject] private GridManager _grid;
    [VContainer.Inject] private VContainer.IObjectResolver _resolver;
    private readonly System.Collections.Generic.HashSet<UnitBase> _vaultUnits = new();
    private bool _vaultUsed;
    private bool _vaultIntermission;
    private double _vaultRemaining;
    private double _vaultElapsed;
    private bool _applicationPaused, _skipResumeFrame;
    /// <summary>런 1회 마지막 유예가 진행 중인지 여부.</summary>
    public bool TimeVaultActive { get; private set; }
    /// <summary>제물의 실제 지출을 +30초 지급과 별도로 표시한다.</summary>
    public event Action<float> OnSacrificePaid;
    /// <summary>시간 금고 시작/종료 시 UI와 전투 등급을 동기화한다.</summary>
    public event Action<bool> OnTimeVaultChanged;
    public void Init()
    {
        
    }

    public event Action        OnTimeUp;
    public event Action<float> OnTimerTick;
    /// <summary>Actual added seconds, after the authoritative countdown has been updated.</summary>
    public event Action<float> OnTimeAdded;
    /// <summary>실제 유닛 시간 변경. 향후 카드 소비 원천도 같은 반응 경로를 사용한다.</summary>
    public event Action<UnitBase, BossBase, double, bool> OnGlitch;
    /// <summary>OnTimeAdded와 같은 순간, 출처와 (있으면) 그 시간을 만든 월드 위치를 함께 — 표시 연출 선택용.</summary>
    public event Action<float, TimeAddSource, Vector3?> OnTimeAddedFrom;

    private readonly CombatCountdown _countdown = new CombatCountdown();
    /// <summary>보스의 도래 효과를 시간초과 전에 처리할 전투 시간 이벤트.</summary>
    public event Action<double> OnCombatTimeAdvanced;
    /// <summary>기존 UI용 남은 시간.</summary>
    public float RemainingTime => (float)(TimeVaultActive ? _vaultRemaining : _countdown.Remaining);
    /// <summary>디버프가 사용하는 실제 누적 전투 시간.</summary>
    public double ElapsedCombatTime => _countdown.Elapsed + _vaultElapsed;
    private void Awake()
    {
        _countdown.OnAdvanced += elapsed =>
        {
            OnCombatTimeAdvanced?.Invoke(elapsed);
            OnTimerTick?.Invoke(RemainingTime);
        };
        _countdown.OnTimeUp += HandleCountdownExpired;
        UnitBase.OnAnyUnitChanged += RefreshVaultTiers;
    }

    private void Update()
    {
        if (TimeVaultActive)
        {
            if (_game?.IsFinished == true) { CancelTimeVault(); return; }
            if (_applicationPaused) return;
            if (_skipResumeFrame) { _skipResumeFrame = false; return; }
            if (!_vaultIntermission) AdvanceTimeVault(Time.unscaledDeltaTime);
            return;
        }
        if (_fieldPause?.AttacksHeld == true) return;
        _countdown.Advance(Time.deltaTime, 1d + (_overdrive?.ExtraTimeDrainRatio ?? 0f));
    }

    public void StartTimer(float seconds)
    {
        CancelTimeVault();
        _vaultUsed = false;
        _vaultIntermission = false;
        _vaultElapsed = 0;
        _countdown.Start(seconds);
        OnTimerTick?.Invoke(RemainingTime);
    }

    public void StopTimer()   => _countdown.Stop();
    /// <summary>다음 보스 등장 전의 대기에서만 유예 시계를 정지한다.</summary>
    public void SetVaultIntermission(bool paused) => _vaultIntermission = paused;

    private void OnApplicationPause(bool paused)
    {
        _applicationPaused = paused;
        if (!paused) _skipResumeFrame = true;
    }

    private void HandleCountdownExpired()
    {
        if (!_vaultUsed && (_overdrive?.TimeVaultSeconds ?? 0f) > 0f && _game?.IsFinished != true)
        {
            _vaultUsed = true;
            TimeVaultActive = true;
            _vaultRemaining = _overdrive.TimeVaultSeconds;
            RefreshVaultTiers();
            OnTimeVaultChanged?.Invoke(true);
            OnTimerTick?.Invoke(RemainingTime);
            return;
        }
        OnTimeUp?.Invoke();
    }

    /// <summary>유예는 일반 회복/시간 지출과 분리된 실제 경과 시계다.</summary>
    public void AdvanceTimeVault(double delta)
    {
        if (delta < 0 || double.IsNaN(delta) || double.IsInfinity(delta)) throw new ArgumentOutOfRangeException(nameof(delta));
        if (!TimeVaultActive || _vaultIntermission || _applicationPaused) return;
        double step = Math.Min(delta, _vaultRemaining);
        _vaultRemaining -= step;
        _vaultElapsed += step;
        OnCombatTimeAdvanced?.Invoke(ElapsedCombatTime);
        // A due damage tick can end the run and cancel the vault.
        if (!TimeVaultActive) return;
        OnTimerTick?.Invoke(RemainingTime);
        if (_vaultRemaining > 0) return;
        CancelTimeVault();
        OnTimeUp?.Invoke();
    }

    /// <summary>런 종료 시 유예와 임시 등급을 정리한다. 사용 횟수는 새 런에서만 복구한다.</summary>
    public void CancelTimeVault()
    {
        if (!TimeVaultActive) return;
        TimeVaultActive = false;
        _vaultRemaining = 0;
        RefreshVaultTiers();
        OnTimeVaultChanged?.Invoke(false);
    }

    private void RefreshVaultTiers()
    {
        if (!TimeVaultActive)
        {
            foreach (var unit in new System.Collections.Generic.List<UnitBase>(_vaultUnits))
                if (unit != null) unit.SetTimeVaultTier(false);
            _vaultUnits.Clear();
            return;
        }
        if (_grid == null) return;
        foreach (var cell in _grid.GetOccupiedCells())
        {
            var unit = cell.OccupyingUnit;
            if (unit == null) continue;
            _vaultUnits.Add(unit);
            unit.SetTimeVaultTier(true);
        }
    }

    /// <summary>과열 압축은 일반 잔고에서만 비살상으로 지불한다.</summary>
    public bool TryPayOverheat(float seconds, UnitBase actor, BossBase target, Action commit = null)
    {
        if (TimeVaultActive || !CanSurviveTimeCost(seconds) || _game?.IsFinished == true) return false;
        _countdown.Spend(seconds, paid => { commit?.Invoke(); PublishGlitch(actor, target, -paid, false); });
        OnTimerTick?.Invoke(RemainingTime);
        return true;
    }
    /// <summary>표시용 float 반올림 없이, 비용 지불 후 양수 잔고가 남는지 조회한다.</summary>
    public bool CanSurviveTimeCost(float seconds) => seconds > 0f && !float.IsInfinity(seconds)
        && !TimeVaultActive && _countdown.Remaining > seconds;

    /// <summary>합성 비용을 즉시 지불한다. 0초면 결과 생성 없이 종결하며 실제 지출만 글리치로 발행한다.</summary>
    public bool SpendTimeFromMerge(float seconds, UnitBase result, BossBase target)
    {
        if (TimeVaultActive) return false;
        if (!(seconds > 0f) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        double actual = _countdown.Spend(seconds, paid => PublishGlitch(result, target, -paid, false));
        OnTimerTick?.Invoke(RemainingTime);
        return actual > 0 && _countdown.Remaining > 0;
    }
    public void ResumeTimer() 
    {
        if (_fieldPause?.AttacksHeld == true) return;
        _countdown.Resume();
    }

    /// <summary>남은 시간을 바로 늘린다. source·origin은 표시 연출 선택에만 쓰인다 (기본 = 보스 처치 합체 연출).</summary>
    public void AddTime(float seconds, TimeAddSource source = TimeAddSource.Default, Vector3? origin = null)
        => AddTimeCore(seconds, source, origin, null, null);

    /// <summary>차트에서 정한 실제 보스 처치 보너스를 지급한 뒤 제물을 정산한다.</summary>
    public void AddBossKillTime(float seconds)
        => AddTimeCore(seconds, TimeAddSource.Default, null, null, null, true);

    /// <summary>디시그망 실제 회복을 원인 유닛과 원래 보스에 귀속한다. 0초 회복은 글리치가 아니다.</summary>
    public void AddTimeFromDisigman(float seconds, Disigman actor, BossBase target)
    {
        if (actor == null || target == null || target.IsDead || _countdown.Remaining <= 0) return;
        AddTimeCore(seconds, TimeAddSource.Hack, actor.transform.position, actor, target);
    }

    private void AddTimeCore(float seconds, TimeAddSource source, Vector3? origin, Disigman actor, BossBase target, bool bossKill = false)
    {
        if (TimeVaultActive || _game?.IsFinished == true) return;
        double before = _countdown.Remaining;
        _countdown.AddTime(seconds);
        double actual = _countdown.Remaining - before;
        if (actor != null && actual > 0) PublishGlitch(actor, target, actual, true);
        OnTimerTick?.Invoke(RemainingTime);
        Debug.Log($"[TimerController] +{seconds}초 추가({source}) → 남은 시간: {RemainingTime:F1}초");
        if (seconds <= 0f) return;
        if (source == TimeAddSource.Default) _audioManager?.PlayCue(GameAudioCue.BossTimeSettlement);
        OnTimeAdded?.Invoke(seconds);
        OnTimeAddedFrom?.Invoke(seconds, source, origin);
        // Commit +30 first, then pay -10 as a distinct source/display event.
        if (bossKill && (_overdrive?.SacrificeCostSeconds ?? 0f) > 0f)
        {
            float cost = _overdrive.SacrificeCostSeconds;
            _countdown.Spend(cost, paid =>
            {
                if (paid == cost) _selections?.RecordSacrifice();
                PublishGlitch(null, null, -paid, false);
                OnSacrificePaid?.Invoke((float)paid);
                if (_resolver != null && _resolver.TryResolve<CenterToast>(out var toast))
                    toast.Show($"제물 -{paid:0.#}초");
            });
            OnTimerTick?.Invoke(RemainingTime);
        }
    }

    private void PublishGlitch(UnitBase actor, BossBase target, double seconds, bool recovery)
    {
        if (OnGlitch == null) return;
        foreach (Action<UnitBase, BossBase, double, bool> handler in OnGlitch.GetInvocationList())
            try { handler(actor, target, seconds, recovery); }
            catch (Exception error) { Debug.LogException(error); }
    }

    private void OnDestroy()
    {
        UnitBase.OnAnyUnitChanged -= RefreshVaultTiers;
        CancelTimeVault();
    }
}
