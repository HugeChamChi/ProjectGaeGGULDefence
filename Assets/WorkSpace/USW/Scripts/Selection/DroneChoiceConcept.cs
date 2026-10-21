using System;

/// <summary>선택지 분류용 태그. 효과·추첨·추천 여부를 결정하지 않는다.</summary>
[Flags]
public enum DroneChoiceConcept
{
    None = 0,
    Common = 1,
    SelfDestruct = 2,
    Glitch = 4,
    Hacking = 8
}
