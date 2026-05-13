using UnityEngine;
using GaeGGUL.Extension;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitInfoPresenter
    {
        private readonly UI_UnitInfoPanel _view;
        private readonly System.Collections.Generic.List<UnitStatUIData> _statBuffer = new System.Collections.Generic.List<UnitStatUIData>(4);

        public UI_UnitInfoPresenter(UI_UnitInfoPanel view)
        {
            _view = view;
        }

        public void SetUnitData(UnitBase unit)
        {
            if (unit == null || unit.unitData == null) return;

            var data = unit.unitData;
            var cell = unit.currentCell;

            _statBuffer.Clear();

            // Atk: 최종값 (기본+보너스), 보너스 텍스트 (+N)
            _statBuffer.Add(new UnitStatUIData(UnitStatType.Atk,           
                cell.GetFinalAttack(data.atk).ToString(),            
                cell.GetAttackBonusText(data.atk)));

            // SkillAtk: 최종값 (기본+보너스), 보너스 텍스트 (+N)
            _statBuffer.Add(new UnitStatUIData(UnitStatType.SkillAtk,      
                cell.GetFinalAttack(data.skillAtk).ToString(),       
                cell.GetAttackBonusText(data.skillAtk)));

            // SkillCooldown: 최종값 (기본+보너스), 보너스 텍스트 (+0.5s)
            float finalCooldown = cell.GetFinalCooldown(data.skillCooldown);
            _statBuffer.Add(new UnitStatUIData(UnitStatType.SkillCooldown, 
                $"{finalCooldown:F1}s",    
                cell.GetCooldownBonusText(data.skillCooldown)));

            // FoodPerTick: 현재는 기본값 표시, 보너스 텍스트 (Buff)
            _statBuffer.Add(new UnitStatUIData(UnitStatType.FoodPerTick,   
                $"+{data.foodPerTick:F0}",      
                cell.GetFoodBonusText()));

            UpdateView(data, _statBuffer);
        }

        public void SetUnitData(UnitData data)
        {
            if (data == null) return;

            _statBuffer.Clear();
            _statBuffer.Add(new UnitStatUIData(UnitStatType.Atk,           data.atk.ToString(),            ""));
            _statBuffer.Add(new UnitStatUIData(UnitStatType.SkillAtk,      data.skillAtk.ToString(),       ""));
            _statBuffer.Add(new UnitStatUIData(UnitStatType.SkillCooldown, $"{data.skillCooldown:F1}s",    ""));
            _statBuffer.Add(new UnitStatUIData(UnitStatType.FoodPerTick,   $"+{data.foodPerTick:F0}",      ""));

            UpdateView(data, _statBuffer);
        }

        private void UpdateView(UnitData data, System.Collections.Generic.IReadOnlyList<UnitStatUIData> stats)
        {
            var tier = data.unitTier;

            _view.UpdateBasicInfo(
                data.unitName, 
                data.unitName,
                data.description, 
                data.icon,
                tier.GetFrame(),
                tier.GetBGColor(),
                tier.GetTextColor()
            );

            _view.UpdateStats(stats);
        }
    }
}
