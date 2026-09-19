using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
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

        [Header("Actions")]
        [SerializeField] private MergeButtonUI mergeButton;
        [SerializeField] private SellButtonUI sellButton;

        public MergeButtonUI MergeButton => mergeButton;
        public SellButtonUI SellButton => sellButton;

        /// <summary>바깥 클릭으로 닫힘 요청 시 발행 (InGameInstaller가 구독)</summary>
        public event Action OnDismissRequested;

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
            bool showActions = !unit.IsWildcardMergeUnit;
            if (mergeButton != null)
            {
                mergeButton.gameObject.SetActive(showActions);
                mergeButton.SetState(showActions && canMerge);
            }
            if (sellButton != null)
            {
                sellButton.gameObject.SetActive(showActions);
                sellButton.SetUnit(showActions ? unit : null);
            }
            Open();
            _isShowing = true;
            _justShown = true;
        }

        public void SetData(UnitData data)
        {
            EnsurePresenter();
            _presenter.SetUnitData(data);
            Open();
            _isShowing = true;
            _justShown = true;
        }

        public override void Close()
        {
            _isShowing = false;
            _presenter?.Clear();
            base.Close();
        }

        /// <summary>닫기 버튼의 직접 비동기 호출도 현재 유닛 추적을 정리한다.</summary>
        public override async UniTask CloseAsync()
        {
            _isShowing = false;
            _presenter?.Clear();
            await base.CloseAsync();
        }

        private void OnDisable()
        {
            _isShowing = false;
            _presenter?.Clear();
        }

        private void LateUpdate()
        {
            if (_isShowing && _presenter != null && !_presenter.Refresh())
            {
                Close();
                OnDismissRequested?.Invoke();
                return;
            }
            if (_justShown) { _justShown = false; return; }
            if (!_isShowing || !Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current == null) return;

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
                _presenter = new UI_UnitInfoPresenter(this);
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

        /// <summary>현재 합산 공격력과 공격간격만 표시한다. 별도 강화/식량 표시는 사용하지 않는다.</summary>
        public void UpdateStats(string atkValue, string atkSpeedValue)
        {
            if (statSlot_Atk != null)      statSlot_Atk.Setup(atkValue);
            if (statSlot_AtkSpeed != null) statSlot_AtkSpeed.Setup(atkSpeedValue);
        }
    }
}
