using UnityEngine;

/// <summary>족장 버튼이 사용하는 스킬 계약. 그리드/유닛/등급을 요구하지 않는다.</summary>
public interface IChiefActiveSkill
{
    Sprite Icon { get; }
    bool IsAvailable { get; }
    bool CanActivate { get; }
    float CooldownProgress { get; }
    float CooldownRemaining { get; }
    bool TryActivate();
}
