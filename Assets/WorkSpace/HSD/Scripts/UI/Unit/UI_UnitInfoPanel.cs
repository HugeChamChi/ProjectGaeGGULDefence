using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GaeGGUL.Extension;

namespace GaeGGUL.UI.Unit
{
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

        public void UpdateStats((UnitStatType type, string value)[] stats)
        {
            if (statSlots == null) return;

            for (int i = 0; i < statSlots.Length && i < stats.Length; i++)
            {
                if (statSlots[i] == null) continue;
                
                // StatExtension을 사용하여 비주얼 정보를 가져옴
                var visual = stats[i].type.GetVisual();
                statSlots[i].Setup(visual, stats[i].value);
            }
        }
    }
}
