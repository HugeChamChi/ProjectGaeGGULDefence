// ════════════════════════════════════════════════════════
// TimerController — InGameSingleton 교체
// ════════════════════════════════════════════════════════
using UnityEngine;
using System;

[DefaultExecutionOrder(-100)]
public class TimerController : MonoBehaviour
{ 
    public void Init()
    {
        
    }

    public event Action        OnTimeUp;
    public event Action<float> OnTimerTick;

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
        _countdown.Resume();
    }

    public void AddTime(float seconds)
    {
        _countdown.AddTime(seconds);
        OnTimerTick?.Invoke(RemainingTime);
        Debug.Log($"[TimerController] +{seconds}초 추가 → 남은 시간: {RemainingTime:F1}초");
    }
}
