using UnityEngine;
using GaeGGUL.Extension;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitInfoPresenter
    {
        private readonly UI_UnitInfoPanel _view;

        public UI_UnitInfoPresenter(UI_UnitInfoPanel view)
        {
            _view = view;
        }

        public void SetUnitData(UnitData data)
        {
            if (data == null) return;

            // 1. 등급 확장 메서드 활용 (GetFrame, GetBGColor, GetTextColor 등)
            var tier = data.unitTier;
            
            // 2. 스텟 정보 구성
            var stats = new (UnitStatType type, string value)[]
            {
                (UnitStatType.Atk,           data.atk.ToString()),
                (UnitStatType.SkillAtk,      data.skillAtk.ToString()),
                (UnitStatType.SkillCooldown, $"{data.skillCooldown:F1}s"),
                (UnitStatType.FoodPerTick,   $"+{data.foodPerTick:F0}")
            };

            // 3. View 업데이트
            // TierExtension을 사용하여 필요한 정보를 직접 뽑아서 넘깁니다.
            _view.UpdateBasicInfo(
                data.unitName, 
                data.unitName, // SkillName (데이터에 아직 없으므로 임시)
                data.description, 
                data.icon, // UnitData의 아이콘 사용
                tier.GetFrame(),
                tier.GetBGColor(),
                tier.GetTextColor()
            );

            _view.UpdateStats(stats);
        }
    }
}
