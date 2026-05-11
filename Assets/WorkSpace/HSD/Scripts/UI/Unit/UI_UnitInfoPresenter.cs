using UnityEngine;
using GaeGGUL.Extension;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitInfoPresenter
    {
        private readonly UI_UnitInfoPanel _view;
        private readonly UnitStatPalette  _statPalette;
        private readonly UnitTierPalette  _tierPalette;

        public UI_UnitInfoPresenter(UI_UnitInfoPanel view, UnitStatPalette statPalette, UnitTierPalette tierPalette)
        {
            _view = view;
            _statPalette = statPalette;
            _tierPalette = tierPalette;
        }

        public void SetUnitData(UnitData data)
        {
            if (data == null) return;

            // 1. 기본 정보 설정 (Tier Palette 활용)
            var tierInfo = _tierPalette?.GetInfo(data.unitTier);
            
            // 2. 스텟 정보 구성
            var stats = new (UnitStatType type, string value)[]
            {
                (UnitStatType.Atk,           data.atk.ToString()),
                (UnitStatType.SkillAtk,      data.skillAtk.ToString()),
                (UnitStatType.SkillCooldown, $"{data.skillCooldown:F1}s"),
                (UnitStatType.FoodPerTick,   $"+{data.foodPerTick:F0}")
            };

            // 3. View 업데이트
            _view.UpdateBasicInfo(
                data.unitName, 
                "data.Skill.Name",
                "data.Skill.Description",
                data.prefab?.GetComponentInChildren<SpriteRenderer>()?.sprite, // 프리팹에서 아이콘 추출 (임시)
                tierInfo
            );

            _view.UpdateStats(stats, _statPalette);
        }
    }
}
