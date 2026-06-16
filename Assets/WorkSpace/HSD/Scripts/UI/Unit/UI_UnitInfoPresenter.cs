using UnityEngine;
using GaeGGUL.Extension;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitInfoPresenter
    {
        private readonly UI_UnitInfoPanel _view;
        private readonly GameDataManager _gdm;

        public UI_UnitInfoPresenter(UI_UnitInfoPanel view, GameDataManager gdm)
        {
            _view = view;
            _gdm = gdm;
        }

        public void SetUnitData(UnitBase unit)
        {
            if (unit == null || unit.unitData == null) return;

            var data = unit.unitData;
            var cell = unit.currentCell;

            string atkValue = cell.GetFinalAttack(data.atk).ToString();
            string atkBonus = cell.GetAttackBonusText(data.atk);

            float finalAtkSpeed = cell.GetFinalAttackSpeed(data.attackSpeed);
            string atkSpeedValue = $"{finalAtkSpeed:F2}s";
            string atkSpeedBonus = cell.GetAttackSpeedBonusText(data.attackSpeed);

            float finalCooldown = cell.GetFinalCooldown(data.skillCooldown);
            string cooldownText = $"{finalCooldown:F1}s";

            string foodValue = $"+{_gdm.GetCurrencyPerSecond(data.characterId):F0}";
            string foodBonus = cell.GetFoodBonusText();

            UpdateView(data, atkValue, atkBonus, atkSpeedValue, atkSpeedBonus, foodValue, foodBonus, cooldownText);
        }

        public void SetUnitData(UnitData data)
        {
            if (data == null) return;

            string atkValue = data.atk.ToString();
            string atkSpeedValue = $"{data.attackSpeed:F2}s";
            string cooldownValue = $"{data.skillCooldown:F1}s";
            string cooldownText = cooldownValue;
            string foodValue = $"+{_gdm.GetCurrencyPerSecond(data.characterId):F0}";

            UpdateView(data, atkValue, "", atkSpeedValue, "", foodValue, "", cooldownText);
        }

        private void UpdateView(UnitData data, string atk, string atkBonus, string atkSpeed, string atkSpeedBonus, string food, string foodBonus, string cooldownText)
        {
            _view.UpdateBasicInfo(data.unitName, data.icon, data.unitTier);
            _view.UpdateSkillInfo(data.skillName, data.description, cooldownText);
            _view.UpdateStats(atk, atkBonus, atkSpeed, atkSpeedBonus, food, foodBonus);
        }
    }
}
