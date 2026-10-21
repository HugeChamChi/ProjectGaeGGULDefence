using UnityEngine;

/// <summary>족장 버튼이 사용하는 스킬 계약. 그리드/유닛/등급을 요구하지 않는다.</summary>
public interface IChiefActiveSkill
{
    /// <summary>현재 보관 중인 충전 완료 횟수.</summary>
    int AvailableCharges { get; }
    /// <summary>순차 충전하여 보관할 수 있는 최대 횟수.</summary>
    int MaximumCharges { get; }
    Sprite Icon { get; }
    bool IsAvailable { get; }
    bool CanActivate { get; }
    float CooldownProgress { get; }
    float CooldownRemaining { get; }
    bool TryActivate();
}
