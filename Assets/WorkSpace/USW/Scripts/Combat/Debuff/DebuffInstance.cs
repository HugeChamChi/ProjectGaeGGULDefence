using System;

/// <summary>보스 하나에 귀속된 효과 상태의 공통 계약.</summary>
public abstract class DebuffInstance
{
    /// <summary>런 동안 유지하는 불변 정의.</summary>
    public DebuffDefinition Definition { get; }
    /// <summary>최근 부여 출처.</summary>
    public int SourceId { get; protected set; }
    /// <summary>현재 스택.</summary>
    public int Stacks { get; protected set; }
    /// <summary>만료 시각. 아머는 대상 수명.</summary>
    public double ExpiresAt { get; protected set; }
    /// <summary>정의 주입.</summary>
    protected DebuffInstance(DebuffDefinition definition) { Definition = definition; }
    /// <summary>효과별 갱신 정책.</summary>
    public abstract void Reapply(double now, DebuffApplyContext context);
    /// <summary>도래 틱 처리. 시간 공급은 대상이 담당한다.</summary>
    public virtual void Advance(double now, Action<long> burnDamage, Func<bool> alive) { }
    /// <summary>틱 처리 후 만료 판정.</summary>
    public bool IsExpired(double now) => now >= ExpiresAt;
}
