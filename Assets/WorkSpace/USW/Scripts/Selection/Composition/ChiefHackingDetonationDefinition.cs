using System;

/// <summary>족장 집결이 보스의 사용 가능한 해킹 스택을 전량 소비해 얻는 추가 피해.</summary>
[Serializable]
public sealed class ChiefHackingDetonationDefinition : SelectionEffectDefinition
{
    /// <summary>소모 스택1개당 집결 기준 피해에 더할 비율.</summary>
    public float DamageRatioPerStack;
}
