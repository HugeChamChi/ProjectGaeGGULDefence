// ════════════════════════════════════════════════════════
// TimerController — InGameSingleton 교체
// ════════════════════════════════════════════════════════
using UnityEngine;
using System;

public class TimerController : MonoBehaviour
{ 
    public void Init()
    {
        
    }

    public event Action        OnTimeUp;
    public event Action<float> OnTimerTick;

    public float RemainingTime { get; private set; }
    private bool _isRunning;

    private void Update()
    {
        if (!_isRunning) return;
        RemainingTime -= Time.deltaTime;
        OnTimerTick?.Invoke(RemainingTime);

        if (RemainingTime <= 0f)
        {
            _isRunning    = false;
            RemainingTime = 0f;
            OnTimeUp?.Invoke();
        }
    }

    public void StartTimer(float seconds)
    {
        RemainingTime = seconds;
        _isRunning    = true;
        OnTimerTick?.Invoke(RemainingTime);
    }

    public void StopTimer()   => _isRunning = false;
    public void ResumeTimer() 
    {
        if (RemainingTime > 0f) _isRunning = true;
    }

    public void AddTime(float seconds)
    {
        RemainingTime += seconds;
        OnTimerTick?.Invoke(RemainingTime);
        Debug.Log($"[TimerController] +{seconds}초 추가 → 남은 시간: {RemainingTime:F1}초");
    }
}
