using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 게임 속도(Time.timeScale)의 단일 소유자 (InGameLifetimeScope, 씬 수명).
///
/// 여러 시스템(레벨업, 토템 선택, 결과창, 설정창, 디버그)이 각자 "요청"만 보내고,
/// 실제 timeScale은 모든 요청 중 **가장 느린 값**이 된다 (요청이 없으면 1배속).
/// → 레벨업 중 설정창을 열었다 닫아도, 레벨업의 정지 요청이 남아 있으므로 게임이 풀리지 않는다.
///
/// 규칙: Time.timeScale을 직접 쓰지 말고 이 서비스를 통한다. (로비 가챠 연출은 인게임 멈춤과 겹치지 않아 예외)
/// </summary>
public sealed class TimeScaleService : IDisposable
{
    private readonly Dictionary<object, float> _requests = new Dictionary<object, float>();

    /// <summary>현재 적용 중인 timeScale.</summary>
    public float Current => Time.timeScale;

    /// <summary>정지(0) 상태인지.</summary>
    public bool IsPaused => Time.timeScale <= 0f;

    /// <summary>적용 값이 바뀔 때 (새 timeScale).</summary>
    public event Action<float> OnChanged;

    /// <summary>씬 시작 시 이전 씬에서 남은 값을 정리한다.</summary>
    public TimeScaleService() => Apply();

    /// <summary>owner의 속도 요청을 설정/갱신한다. 0 = 정지, 0~1 = 슬로우모션.</summary>
    public void Request(object owner, float scale)
    {
        if (owner == null) return;
        _requests[owner] = Mathf.Max(0f, scale);
        Apply();
    }

    /// <summary>owner의 정지 요청 (= Request(owner, 0)).</summary>
    public void Pause(object owner) => Request(owner, 0f);

    /// <summary>owner의 요청을 해제한다. 남은 요청이 없으면 1배속.</summary>
    public void Release(object owner)
    {
        if (owner != null && _requests.Remove(owner)) Apply();
    }

    /// <summary>owner가 요청 중인지.</summary>
    public bool IsRequesting(object owner) => owner != null && _requests.ContainsKey(owner);

    /// <summary>모든 요청을 해제하고 1배속으로 (씬 이동 직전 등).</summary>
    public void ReleaseAll()
    {
        _requests.Clear();
        Apply();
    }

    private void Apply()
    {
        float scale = 1f;
        foreach (var value in _requests.Values)
            if (value < scale) scale = value;
        if (Mathf.Approximately(Time.timeScale, scale)) return;
        Time.timeScale = scale;
        OnChanged?.Invoke(scale);
    }

    /// <summary>씬 종료 시 1배속으로 복구한다.</summary>
    public void Dispose()
    {
        _requests.Clear();
        Time.timeScale = 1f;
    }
}
