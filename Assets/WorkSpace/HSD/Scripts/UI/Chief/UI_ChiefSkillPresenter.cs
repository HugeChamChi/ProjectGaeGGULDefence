using UnityEngine;

public class UI_ChiefSkillPresenter
{
    private readonly UI_ChiefSkillButtonView _view;
    private ChiefUnit _chiefUnit;

    public UI_ChiefSkillPresenter(UI_ChiefSkillButtonView view)
    {
        _view = view;
        _view.BindSkillButton(ExecuteSkill);
    }

    public void SetChiefUnit(ChiefUnit chiefUnit)
    {
        _chiefUnit = chiefUnit;
    }

    public void OnUpdate(float deltaTime)
    {
        if (_chiefUnit == null || _chiefUnit.unitData == null || !_chiefUnit.gameObject.activeInHierarchy || _chiefUnit.currentCell == null)
        {
            _view.SetCooldownFillAmount(1f);
            _view.SetCooldownText("");
            _view.SetButtonInteractable(false);
            return;
        }

        float interval = _chiefUnit.GetCurrentSkillInterval();
        float timer = _chiefUnit.CurrentSkillTimer;
        float progress = _chiefUnit.SkillGaugeProgress; // 0f ~ 1f
        bool isReady = _chiefUnit.IsSkillReady; // progress >= 1f

        // Fill이 0일 때가 사용 가능하도록 역전 (1 - progress)
        _view.SetCooldownFillAmount(Mathf.Clamp01(1f - progress));
        _view.SetButtonInteractable(isReady);

        if (isReady)
        {
            _view.SetCooldownText(""); // 사용 가능할 때 텍스트 제거
        }
        else
        {
            float remainTime = Mathf.Max(0f, interval - timer);
            _view.SetCooldownText($"{remainTime:F1}s");
        }
    }

    public void ExecuteSkill()
    {
        if (_chiefUnit != null && _chiefUnit.gameObject.activeInHierarchy && _chiefUnit.currentCell != null && _chiefUnit.IsSkillReady)
        {
            _chiefUnit.ExecuteSkillManually();
        }
    }
}
