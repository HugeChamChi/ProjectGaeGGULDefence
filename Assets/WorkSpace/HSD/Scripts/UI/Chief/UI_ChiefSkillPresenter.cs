using UnityEngine;

public class UI_ChiefSkillPresenter
{
    private readonly UI_ChiefSkillButtonView _view;
    private IChiefActiveSkill _skill;

    public UI_ChiefSkillPresenter(UI_ChiefSkillButtonView view)
    {
        _view = view;
        _view.BindSkillButton(ExecuteSkill);
    }

    public void SetChiefUnit(ChiefUnit chiefUnit)
    {
        SetActiveSkill(chiefUnit != null ? new UnitChiefActiveSkill(chiefUnit) : null);
    }
    /// <summary>그리드 유무와 관계없는 액티브 스킬을 표시한다.</summary>
    public void SetActiveSkill(IChiefActiveSkill skill)
    {
        _skill=skill;
        _view.SetIcon(skill?.Icon);
        OnUpdate(0);
    }

    public void OnUpdate(float deltaTime)
    {
        if (_skill == null || !_skill.IsAvailable)
        {
            _view.SetCooldownSliderValue(1f);
            _view.SetCooldownText("");
            _view.SetButtonInteractable(false);
            _view.SetDisabledVisual(true);
            return;
        }

        float progress = _skill.CooldownProgress;
        bool isReady = _skill.CanActivate;

        // 슬라이더 값이 0일 때가 사용 가능하도록 역전 (1 - progress)
        _view.SetCooldownSliderValue(Mathf.Clamp01(1f - progress));
        _view.SetButtonInteractable(isReady);
        _view.SetDisabledVisual(!isReady);

        if (_skill.CooldownRemaining <= 0)
        {
            _view.SetCooldownText(""); // 사용 가능할 때 텍스트 제거
        }
        else
        {
            float remainTime = _skill.CooldownRemaining;
            _view.SetCooldownText($"{remainTime:F1}s");
        }
    }

    public void ExecuteSkill()
    {
        _skill?.TryActivate();
    }
}
