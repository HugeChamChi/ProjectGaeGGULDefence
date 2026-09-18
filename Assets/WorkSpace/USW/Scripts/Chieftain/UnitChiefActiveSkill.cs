using UnityEngine;

/// <summary>기존 그리드 족장의 수동 스킬을 공통 버튼 계약으로 연결한다.</summary>
public sealed class UnitChiefActiveSkill : IChiefActiveSkill
{
    private readonly ChiefUnit _unit;
    /// <summary>기존 유닛 참조를 보관한다.</summary>
    public UnitChiefActiveSkill(ChiefUnit unit) => _unit = unit;
    /// <inheritdoc />
    public Sprite Icon => _unit != null ? _unit.unitData?.icon : null;
    /// <inheritdoc />
    public bool IsAvailable => _unit != null && _unit.unitData != null && _unit.isActiveAndEnabled && _unit.currentCell != null;
    /// <inheritdoc />
    public bool CanActivate => IsAvailable && Time.timeScale > 0 && _unit.IsSkillReady;
    /// <inheritdoc />
    public float CooldownProgress => IsAvailable ? _unit.SkillGaugeProgress : 0;
    /// <inheritdoc />
    public float CooldownRemaining => IsAvailable ? Mathf.Max(0, _unit.GetCurrentSkillInterval() - _unit.CurrentSkillTimer) : 0;
    /// <inheritdoc />
    public bool TryActivate()
    {
        if (!CanActivate) return false;
        _unit.ExecuteSkillManually();
        return true;
    }
}
