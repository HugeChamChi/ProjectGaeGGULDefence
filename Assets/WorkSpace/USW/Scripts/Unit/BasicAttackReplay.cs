using System;
using UnityEngine;

/// <summary>일반 공격 한 시퀀스의 그림자 발사 기록. 스킬/보너스 공격 이벤트를 재실행하지 않는다.</summary>
public sealed class BasicAttackReplay
{
    /// <summary>원본 공격자. 판매로 파괴되면 재현하지 않는다.</summary>
    public UnitBase Source { get; }
    /// <summary>처음 지정한 대상. 다음 보스로 바꾸지 않는다.</summary>
    public BossBase Target { get; }
    /// <summary>공격 시작 시 위치.</summary>
    public Vector3 Origin { get; }
    private Action<Action, bool> _schedule;
    private bool _hasShot;

    /// <summary>원본 공격 시점의 유닛과 대상을 저장한다.</summary>
    public BasicAttackReplay(UnitBase source, BossBase target)
    {
        Source = source;
        Target = target;
        Origin = source != null ? source.transform.position : Vector3.zero;
    }

    /// <summary>겹친 그림자 범위는 한 번만 예약한다.</summary>
    public bool Enable(Action<Action, bool> schedule)
    {
        if (_schedule != null || schedule == null) return false;
        _schedule = schedule;
        return true;
    }

    /// <summary>그림자 토템에 의해 활성화되었는지 여부.</summary>
    public bool IsEnabled => _schedule != null;
    /// <summary>이동과 무관하게 원래 대상/공격자가 존재할 때만 재현 가능하다.</summary>
    public bool CanReplay => Source != null && Target != null && !Target.IsDead;

    /// <summary>원본과 같은 투사체 구성/목적지/적중 콜백을 한 번 예약한다.</summary>
    public void RecordShot(ProjectilePool pool, Vector3 from, Vector3 to, ProjectileData data,
        float size, Action onHit)
    {
        if (_schedule == null) return;
        bool first = !_hasShot;
        _hasShot = true;
        _schedule(() =>
        {
            if (!CanReplay || pool == null) return;
            Action guardedHit = () => { if (CanReplay) onHit?.Invoke(); };
            if (data != null) pool.Launch(from, to, data, guardedHit, Source, size);
            else pool.Launch(from, to, guardedHit, Source, size);
        }, first);
    }
}
