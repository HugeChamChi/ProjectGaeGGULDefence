using System;
using UnityEngine;

/// <summary>효과 정의의 FK와 부여 조건. 조회 시 값 복사로 반환한다.</summary>
[Serializable]
public struct DebuffBinding
{
    [SerializeField] private int _debuffId;
    [SerializeField] private DebuffTrigger _trigger;
    [SerializeField] private int _stacksPerApply;
    /// <summary>0이면 디버프 설정 없음.</summary>
    public int DebuffId => _debuffId;
    /// <summary>설정된 발동 시점.</summary>
    public DebuffTrigger Trigger => _trigger;
    /// <summary>한 번에 부여할 스택.</summary>
    public int StacksPerApply => _stacksPerApply;
    /// <summary>참조가 설정되었는지 여부.</summary>
    public bool IsConfigured => _debuffId > 0;
    /// <summary>유효한 디버프 연결을 생성한다.</summary>
    public DebuffBinding(int id, DebuffTrigger trigger, int stacks)
    {
        if (id <= 0 || trigger == DebuffTrigger.None || !Enum.IsDefined(typeof(DebuffTrigger), trigger) || stacks <= 0)
            throw new ArgumentException("Invalid debuff binding.");
        _debuffId = id; _trigger = trigger; _stacksPerApply = stacks;
    }
}
