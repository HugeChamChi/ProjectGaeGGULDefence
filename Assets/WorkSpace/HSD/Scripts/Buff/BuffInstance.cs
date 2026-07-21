using UnityEngine;

/// <summary>
/// 런타임에 유닛에 걸린 버프 1건의 상태(스택 수, 만료 시각). MonoBehaviour가 아닌 순수 C# 클래스이며
/// BuffController가 리스트로 보관/관리한다.
/// </summary>
public class BuffInstance
{
    public BuffData Def { get; }
    public UnitBase Source { get; }
    public int StackCount { get; private set; }
    public float ExpireTime { get; private set; }

    public BuffInstance(BuffData def, UnitBase source)
    {
        Def = def;
        Source = source;
        StackCount = def.stackPolicy != null ? def.stackPolicy.Clamp(1) : 1;
        ExpireTime = ComputeExpireTime();
    }

    public bool IsExpired => Def.duration > 0f && Time.time >= ExpireTime;

    /// <summary>동일 버프가 재적용될 때 호출 — 스택 정책에 따라 스택 수와 (필요 시) 만료 시각을 갱신한다.
    /// 스택별 개별 타이머가 아닌 버프 1건 공유 타이머로 동작한다.</summary>
    public void Reapply(IBuffStackPolicy policy)
    {
        if (policy == null) return;

        StackCount = policy.Clamp(policy.OnReapply(StackCount));

        if (policy.RefreshDurationOnReapply)
            ExpireTime = ComputeExpireTime();
    }

    private float ComputeExpireTime() => Def.duration > 0f ? Time.time + Def.duration : float.PositiveInfinity;
}
