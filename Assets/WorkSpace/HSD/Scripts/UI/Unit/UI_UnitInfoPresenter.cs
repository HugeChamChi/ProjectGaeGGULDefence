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
            var tier = unit.currentTier;

            string atkValue = cell.GetFinalAttack(data.atk.Get(tier)).ToString();
            string atkBonus = cell.GetAttackBonusText(data.atk.Get(tier));

            float finalAtkSpeed = cell.GetFinalAttackSpeed(data.attackSpeed.Get(tier));
            string atkSpeedValue = $"{finalAtkSpeed:F2}s";
            string atkSpeedBonus = cell.GetAttackSpeedBonusText(data.attackSpeed.Get(tier));

            float finalCooldown = cell.GetFinalCooldown(data.skillCooldown.Get(tier));
            string cooldownText = $"{finalCooldown:F1}s";

            string foodValue = $"+{_gdm.GetCurrencyPerSecond(data.characterId.Get(tier)):F0}";
            string foodBonus = cell.GetFoodBonusText();

            UpdateView(data, tier, atkValue, atkBonus, atkSpeedValue, atkSpeedBonus, foodValue, foodBonus, cooldownText);
        }

        public void SetUnitData(UnitData data)
        {
            if (data == null) return;
            var tier = Tier.Normal;

            string atkValue = data.atk.Get(tier).ToString();
            string atkSpeedValue = $"{data.attackSpeed.Get(tier):F2}s";
            string cooldownValue = $"{data.skillCooldown.Get(tier):F1}s";
            string cooldownText = cooldownValue;
            string foodValue = $"+{_gdm.GetCurrencyPerSecond(data.characterId.Get(tier)):F0}";

            UpdateView(data, tier, atkValue, "", atkSpeedValue, "", foodValue, "", cooldownText);
        }

        private void UpdateView(UnitData data, Tier tier, string atk, string atkBonus, string atkSpeed, string atkSpeedBonus, string food, string foodBonus, string cooldownText)
        {
            _view.UpdateBasicInfo(data.unitName, data.icon, tier);

            // skillData가 있으면 그쪽이 스킬의 단일 소스(skillName/description)이고,
            // 없으면 OnSkillFull() 오버라이드로 스킬을 구현하는 구형 유닛(무직/드론 등)을 위해
            // UnitData.description만 폴백으로 쓴다.
            string skillName = data.skillData != null ? data.skillData.skillName : string.Empty;
            string skillDescription = data.skillData != null ? data.skillData.description : data.description;
            _view.UpdateSkillInfo(skillName, skillDescription, cooldownText);

            _view.UpdateStats(atk, atkBonus, atkSpeed, atkSpeedBonus, food, foodBonus);
        }
    }
}
