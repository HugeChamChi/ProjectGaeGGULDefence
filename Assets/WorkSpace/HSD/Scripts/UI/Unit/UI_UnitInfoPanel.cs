using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GaeGGUL.Extension;
using GaeGGUL.UI.Common;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitInfoPanel : UI_Base
    {
        [Header("Basic Info")]
        [SerializeField] private UI_IconTierSlot iconSlot;
        [SerializeField] private TextMeshProUGUI txt_UnitTier;
        [SerializeField] private TextMeshProUGUI txt_UnitName;

        [Header("Skill")]
        [SerializeField] private TextMeshProUGUI txt_SkillNameText;
        [SerializeField] private TextMeshProUGUI txt_SkillDescription;
        [SerializeField] private TextMeshProUGUI txt_SkillCooldown;

        [Header("Stats")]
        [SerializeField] private UI_StatSlot statSlot_Atk;
        [SerializeField] private UI_StatSlot statSlot_AtkSpeed;
        [SerializeField] private UI_StatSlot statSlot_Food;

        [VContainer.Inject] public GameDataManager _gameDataManager;
        private UI_UnitInfoPresenter _presenter;

        protected override void Awake()
        {
            base.Awake();
            EnsurePresenter();
        }

        public void SetData(UnitBase unit)
        {
            if (unit == null) return;
            EnsurePresenter();
            _presenter.SetUnitData(unit);
            Open();
        }

        public void SetData(UnitData data)
        {
            EnsurePresenter();
            _presenter.SetUnitData(data);
            Open();
        }

        private void EnsurePresenter()
        {
            if (_presenter == null)
            {
                _presenter = new UI_UnitInfoPresenter(this, _gameDataManager);
            }
        }

        public void UpdateBasicInfo(string unitName, Sprite icon, Tier tier)
        {
            if (txt_UnitTier != null) txt_UnitTier.text = $"[{tier.ToString()}]".ToColor(tier.GetTextColor());
            if (txt_UnitName != null) txt_UnitName.text = unitName;
            if (iconSlot != null)     iconSlot.SetData(icon, tier);
        }

        public void UpdateSkillInfo(string skillName, string skillDescription, string cooldownText)
        {
            if (txt_SkillNameText != null)    txt_SkillNameText.text = skillName;
            if (txt_SkillDescription != null) txt_SkillDescription.text = skillDescription;
            if (txt_SkillCooldown != null) txt_SkillCooldown.text = cooldownText;
        }

        public void UpdateStats(string atkValue, string atkBonus, string atkSpeedValue, string atkSpeedBonus, string foodValue, string foodBonus)
        {
            if (statSlot_Atk != null)      statSlot_Atk.Setup(atkValue, atkBonus);
            if (statSlot_AtkSpeed != null) statSlot_AtkSpeed.Setup(atkSpeedValue, atkSpeedBonus);
            if (statSlot_Food != null)     statSlot_Food.Setup(foodValue, foodBonus);
        }
    }
}
