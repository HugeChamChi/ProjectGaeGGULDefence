// ════════════════════════════════════════════════════════
// TimerController — InGameSingleton 교체
// ════════════════════════════════════════════════════════
using UnityEngine;
using System;

[DefaultExecutionOrder(-100)]
public class TimerController : MonoBehaviour
{ 
    [VContainer.Inject] private FieldPauseVisuals _fieldPause;
    public void Init()
    {
        
    }

    public event Action        OnTimeUp;
    public event Action<float> OnTimerTick;
    /// <summary>Actual added seconds, after the authoritative countdown has been updated.</summary>
    public event Action<float> OnTimeAdded;
    /// <summary>OnTimeAdded와 같은 순간, 출처와 (있으면) 그 시간을 만든 월드 위치를 함께 — 표시 연출 선택용.</summary>
    public event Action<float, TimeAddSource, Vector3?> OnTimeAddedFrom;

    private readonly CombatCountdown _countdown = new CombatCountdown();
    /// <summary>보스의 도래 효과를 시간초과 전에 처리할 전투 시간 이벤트.</summary>
    public event Action<double> OnCombatTimeAdvanced;
    /// <summary>기존 UI용 남은 시간.</summary>
    public float RemainingTime => (float)_countdown.Remaining;
    /// <summary>디버프가 사용하는 실제 누적 전투 시간.</summary>
    public double ElapsedCombatTime => _countdown.Elapsed;
    private void Awake()
    {
        _countdown.OnAdvanced += elapsed =>
        {
            OnCombatTimeAdvanced?.Invoke(elapsed);
            OnTimerTick?.Invoke(RemainingTime);
        };
        _countdown.OnTimeUp += () => OnTimeUp?.Invoke();
    }

    private void Update()
    {
        if (_fieldPause?.AttacksHeld == true) return;
        _countdown.Advance(Time.deltaTime);
    }

    public void StartTimer(float seconds)
    {
        _countdown.Start(seconds);
        OnTimerTick?.Invoke(RemainingTime);
    }

    public void StopTimer()   => _countdown.Stop();
    public void ResumeTimer() 
    {
        if (_fieldPause?.AttacksHeld == true) return;
        _countdown.Resume();
    }

    /// <summary>남은 시간을 바로 늘린다. source·origin은 표시 연출 선택에만 쓰인다 (기본 = 보스 처치 합체 연출).</summary>
    public void AddTime(float seconds, TimeAddSource source = TimeAddSource.Default, Vector3? origin = null)
    {
        _countdown.AddTime(seconds);
        OnTimerTick?.Invoke(RemainingTime);
        Debug.Log($"[TimerController] +{seconds}초 추가({source}) → 남은 시간: {RemainingTime:F1}초");
        if (seconds <= 0f) return;
        OnTimeAdded?.Invoke(seconds);
        OnTimeAddedFrom?.Invoke(seconds, source, origin);
    }
}
