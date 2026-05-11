using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitInfoPanel : UI_Base
    {
        [Header("Palettes")]
        [SerializeField] private UnitStatPalette statPalette;
        [SerializeField] private UnitTierPalette tierPalette;

        [Header("Basic Info")]
        [SerializeField] private Image           img_UnitIcon;
        [SerializeField] private Image           img_TierFrame;
        [SerializeField] private Image           img_TierBG;
        [SerializeField] private TextMeshProUGUI txt_UnitName;
        [SerializeField] private TextMeshProUGUI txt_SkillNameText;
        [SerializeField] private TextMeshProUGUI txt_SkillDescription;

        [Header("Stats")]
        [SerializeField] private UI_UnitStatSlot[] statSlots; // 고정 4개 예상

        private UI_UnitInfoPresenter _presenter;

        protected override void Awake()
        {
            base.Awake();
            _presenter = new UI_UnitInfoPresenter(this, statPalette, tierPalette);
        }

        public void SetData(UnitData data)
        {
            _presenter.SetUnitData(data);
        }

        public void UpdateBasicInfo(string unitName, string skillName, string skillDescription, Sprite icon, UnitTierUIInfo tierInfo)
        {
            if (txt_UnitName != null)           txt_UnitName.text = unitName;
            if (txt_SkillNameText != null)      txt_SkillNameText.text = skillName;
            if (txt_SkillDescription != null)   txt_SkillDescription.text = skillDescription;
            if (img_UnitIcon != null)           img_UnitIcon.sprite = icon;

            if (tierInfo != null)
            {
                if (img_TierFrame != null) img_TierFrame.sprite = tierInfo.frameSprite;
                if (img_TierBG != null)    img_TierBG.color = tierInfo.bgColor;
                
                // Tier Name 색상 적용 예시 (Extension 사용)
                // txt_UnitName.text = unitName.ToColor(tierInfo.textColor);
            }
        }

        public void UpdateStats((UnitStatType type, string value)[] stats, UnitStatPalette palette)
        {
            for (int i = 0; i < statSlots.Length && i < stats.Length; i++)
            {
                var visual = palette.GetInfo(stats[i].type);
                statSlots[i].Setup(visual, stats[i].value);
            }
        }
    }
}
