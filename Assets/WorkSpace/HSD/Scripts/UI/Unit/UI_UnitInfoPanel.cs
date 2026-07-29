using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GaeGGUL.Extension;
using GaeGGUL.UI.Common;

namespace GaeGGUL.UI.Unit
{
    public class UI_UnitInfoPanel : UI_Base
    {
        [Header("Basic Info")]
        [SerializeField] private UI_IconTierSlot iconSlot;
        [SerializeField] private TextMeshProUGUI txt_UnitName;

        [Header("Skill")]
        [SerializeField] private TextMeshProUGUI txt_SkillNameText;
        [SerializeField] private TextMeshProUGUI txt_SkillDescription;
        [SerializeField] private TextMeshProUGUI txt_SkillCooldown;

        [Header("Stats")]
        [SerializeField] private UI_StatSlot statSlot_Atk;
        [SerializeField] private UI_StatSlot statSlot_AtkSpeed;
        [SerializeField] private UI_StatSlot statSlot_Food;

        [Header("Actions")]
        [SerializeField] private MergeButtonUI mergeButton;
        [SerializeField] private SellButtonUI sellButton;

        public MergeButtonUI MergeButton => mergeButton;
        public SellButtonUI SellButton => sellButton;

        /// <summary>바깥 클릭으로 닫힘 요청 시 발행 (InGameInstaller가 구독)</summary>
        public event Action OnDismissRequested;

        [VContainer.Inject] public GameDataManager _gameDataManager;
        private UI_UnitInfoPresenter _presenter;

        private bool _isShowing;
        private bool _justShown;

        protected override void Awake()
        {
            base.Awake();
            EnsurePresenter();
        }

        public void SetData(UnitBase unit, bool canMerge = true)
        {
            if (unit == null) return;
            EnsurePresenter();
            _presenter.SetUnitData(unit);
            if (mergeButton != null) mergeButton.SetState(canMerge);
            if (sellButton != null) sellButton.SetUnit(unit);
            Open();
            _isShowing = true;
            _justShown = true;
        }

        public void SetData(UnitData data)
        {
            EnsurePresenter();
            _presenter.SetUnitData(data);
            Open();
        }

        public override void Close()
        {
            base.Close();
            _isShowing = false;
        }

        private void LateUpdate()
        {
            if (_justShown) { _justShown = false; return; }
            if (!_isShowing || !Input.GetMouseButtonDown(0)) return;

            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);

            foreach (var r in results)
            {
                if (r.gameObject.transform.IsChildOf(transform)) return;
            }

            _isShowing = false;
            OnDismissRequested?.Invoke();
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
