using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GaeGGUL.Extension;

namespace GaeGGUL.UI.Unit
{
    public struct UnitStatUIData
    {
        public UnitStatType Type;
        public string Value;
        public string Bonus;

        public UnitStatUIData(UnitStatType type, string value, string bonus)
        {
            Type = type;
            Value = value;
            Bonus = bonus;
        }
    }

    public class UI_UnitInfoPanel : MonoBehaviour
    {
        [Header("Basic Info")]
        [SerializeField] private Image           img_UnitIcon;
        [SerializeField] private Image           img_TierFrame;
        [SerializeField] private Image           img_TierBG;
        [SerializeField] private TextMeshProUGUI txt_UnitName;
        [SerializeField] private TextMeshProUGUI txt_SkillNameText;
        [SerializeField] private TextMeshProUGUI txt_SkillDescription;

        [Header("Stats")]
        [SerializeField] private UI_UnitStatSlot[] statSlots; 

        private UI_UnitInfoPresenter _presenter;

        protected void Awake()
        {
            EnsurePresenter();
        }

        public void SetData(UnitBase unit)
        {
            if (unit == null) return;
            EnsurePresenter();
            _presenter.SetUnitData(unit);
            gameObject.SetActive(true);
        }

        public void SetData(UnitData data)
        {
            EnsurePresenter();
            _presenter.SetUnitData(data);
            gameObject.SetActive(true);
        }

        private void EnsurePresenter()
        {
            if (_presenter == null)
            {
                _presenter = new UI_UnitInfoPresenter(this);
            }
        }

        public void UpdateBasicInfo(string unitName, string skillName, string skillDescription, Sprite icon, Sprite frame, Color bgColor, Color textColor)
        {
            if (txt_UnitName != null)         txt_UnitName.text = unitName.ToColor(textColor); // 이름에 등급색 적용
            if (txt_SkillNameText != null)    txt_SkillNameText.text = skillName;
            if (txt_SkillDescription != null) txt_SkillDescription.text = skillDescription;
            
            if (img_UnitIcon != null)         img_UnitIcon.sprite = icon;
            if (img_TierFrame != null)        img_TierFrame.sprite = frame;
            if (img_TierBG != null)           img_TierBG.color = bgColor;
        }

        public void UpdateStats(System.Collections.Generic.IReadOnlyList<UnitStatUIData> stats)
        {
            if (statSlots == null) return;

            for (int i = 0; i < statSlots.Length && i < stats.Count; i++)
            {
                if (statSlots[i] == null) continue;
                
                var statData = stats[i];
                var visual = statData.Type.GetVisual();
                statSlots[i].Setup(visual, statData.Value, statData.Bonus);
            }
        }
    }
}
