using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using GaeGGUL.UI.Common;
using GaeGGUL.Extension;

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
        [SerializeField] private SellButtonUI sellButton;

        public SellButtonUI SellButton => sellButton;

        /// <summary>바깥 클릭으로 닫힘 요청 시 발행 (InGameInstaller가 구독)</summary>
        public event Action OnDismissRequested;

        private UI_UnitInfoPresenter _presenter;
        private Toggle _descriptionToggle;
        private DebuffInfoLink _effectLink;
        private RectTransform _levelProgressPanel;
        private TextMeshProUGUI _levelProgressText;

        /// <summary>Supplies the shared explanation presenter explicitly from scene wiring.</summary>
        [VContainer.Inject]
        public void ConfigureEffectInfo(DebuffInfoPresenter presenter)
        {
            if (txt_SkillDescription == null) return;
            _effectLink = txt_SkillDescription.GetComponent<DebuffInfoLink>() ?? txt_SkillDescription.gameObject.AddComponent<DebuffInfoLink>();
            _effectLink.Configure(this, presenter);
        }

        private bool _isShowing;
        private bool _justShown;
        private bool _upgradeFeedback;
        private float _previousAttack, _previousInterval;
        private int _previousSortingOrder;
        private bool _previousOverrideSorting;
        private Canvas _feedbackCanvas;
        private bool? _foodStatMode;
        private Image _firstStatIcon, _secondStatIcon;
        private Sprite _attackIcon, _speedIcon;

        public void ShowUpgradeFeedback(UnitBase unit, float attack, float interval)
        {
            SetData(unit);
            if (_feedbackCanvas == null)
            {
                _feedbackCanvas = GetComponent<Canvas>();
                if (_feedbackCanvas == null) _feedbackCanvas = gameObject.AddComponent<Canvas>();
                if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            }
            _previousSortingOrder = _feedbackCanvas.sortingOrder;
            _previousOverrideSorting = _feedbackCanvas.overrideSorting;
            _upgradeFeedback = true;
            _previousAttack = attack;
            _previousInterval = interval;
            _presenter.SetDescriptionMode(false);
            if (sellButton != null) sellButton.gameObject.SetActive(false);
        }

        protected override void Awake()
        {
            base.Awake();
            EnsurePresenter();
        }

        /// <summary>Shows a placed unit with a brief explanation and live combat values.</summary>
        public void SetData(UnitBase unit, bool canMerge = true)
        {
            if (unit == null) return;
            RestoreFeedbackSorting();
            _effectLink?.Clear();
            EnsureDescriptionToggle();
            _presenter.SetDescriptionMode(false);
            _descriptionToggle.SetIsOnWithoutNotify(false);
            _presenter.SetUnitData(unit);
            bool showActions = !unit.IsWildcardMergeUnit;
            if (sellButton != null)
            {
                sellButton.gameObject.SetActive(showActions);
                sellButton.SetUnit(showActions ? unit : null);
            }
            gameObject.SetActive(true);
            _canvas = InfoPanelPresentation.EnsureCanvas(gameObject);
            Open();
            _isShowing = true;
            _justShown = true;
        }

        /// <summary>Shows authored unit information with the brief explanation selected.</summary>
        public void SetData(UnitData data)
        {
            RestoreFeedbackSorting();
            _effectLink?.Clear();
            EnsureDescriptionToggle();
            _presenter.SetDescriptionMode(false);
            _descriptionToggle.SetIsOnWithoutNotify(false);
            _presenter.SetUnitData(data);
            if (sellButton != null)
            {
                sellButton.SetUnit(null);
                sellButton.gameObject.SetActive(false);
            }
            gameObject.SetActive(true);
            _canvas = InfoPanelPresentation.EnsureCanvas(gameObject);
            Open();
            _isShowing = true;
            _justShown = true;
        }

        public override void Close()
        {
            _effectLink?.Clear();
            _isShowing = false;
            _presenter?.Clear();
            base.Close();
        }

        /// <summary>닫기 버튼의 직접 비동기 호출도 현재 유닛 추적을 정리한다.</summary>
        public override async UniTask CloseAsync()
        {
            _effectLink?.Clear();
            _isShowing = false;
            _presenter?.Clear();
            await base.CloseAsync();
            RestoreFeedbackSorting();
        }

        private void RestoreFeedbackSorting()
        {
            if (!_upgradeFeedback) return;
            _feedbackCanvas.sortingOrder = _previousSortingOrder;
            _feedbackCanvas.overrideSorting = _previousOverrideSorting;
            _upgradeFeedback = false;
        }

        private void OnDisable()
        {
            _effectLink?.Clear();
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
            if (_effectLink?.BlocksOwnerInput == true) return;
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
            if (_upgradeFeedback) Close();
            OnDismissRequested?.Invoke();
        }

        private void EnsureDescriptionToggle()
        {
            EnsurePresenter();
            if (_descriptionToggle != null) return;
            _canvas = InfoPanelPresentation.EnsureCanvas(gameObject);
            _descriptionToggle = InfoPanelPresentation.CreateToggle(transform, txt_SkillDescription,
                detailed => _presenter.SetDescriptionMode(detailed));
            ((RectTransform)_descriptionToggle.transform).anchoredPosition = new Vector2(-20f, -72f);
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

        public void UpdateSkillInfo(string skillName, string skillDescription, string cooldownText, DebuffBinding binding = default)
        {
            if (txt_SkillNameText != null)    txt_SkillNameText.text = skillName;
            if (txt_SkillDescription != null) txt_SkillDescription.text = _effectLink != null ? _effectLink.SetDescription(skillDescription, binding) : skillDescription;
            if (txt_SkillCooldown != null)
            {
                txt_SkillCooldown.text = cooldownText;
            }
        }

        /// <summary>Shows compact milestone rows in an attached footer below the skill box.</summary>
        public void UpdateLevelProgress(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (_levelProgressPanel != null) _levelProgressPanel.gameObject.SetActive(false);
                return;
            }
            if (_levelProgressPanel == null)
            {
                var go = new GameObject("LevelProgress", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                _levelProgressPanel = (RectTransform)go.transform;
                _levelProgressPanel.anchorMin = Vector2.zero;
                _levelProgressPanel.anchorMax = new Vector2(1f, 0f);
                _levelProgressPanel.pivot = new Vector2(.5f, 1f);
                _levelProgressPanel.anchoredPosition = new Vector2(0f, -4f);
                var background = go.GetComponent<Image>();
                var source = GetComponent<Image>();
                if (source != null)
                {
                    background.sprite = source.sprite;
                    background.type = source.type;
                    background.color = source.color;
                    background.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                }
                else background.color = new Color(.12f, .12f, .12f, 1f);
                var label = new GameObject("Milestones", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(go.transform, false);
                _levelProgressText = label.GetComponent<TextMeshProUGUI>();
                if (txt_SkillDescription != null) _levelProgressText.font = txt_SkillDescription.font;
                _levelProgressText.fontSize = UiTypography.BodySize * .75f;
                _levelProgressText.fontSizeMax = UiTypography.BodySize * .75f;
                _levelProgressText.fontSizeMin = 18f;
                _levelProgressText.enableAutoSizing = true;
                _levelProgressText.textWrappingMode = TextWrappingModes.NoWrap;
                _levelProgressText.richText = true;
                _levelProgressText.color = Color.white;
                _levelProgressText.alignment = TextAlignmentOptions.TopLeft;
                _levelProgressText.raycastTarget = false;
                var rect = _levelProgressText.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(20f, 10f); rect.offsetMax = new Vector2(-20f, -10f);
            }
            _levelProgressPanel.gameObject.SetActive(true);
            _levelProgressText.text = text;
            float width = Mathf.Max(1f, ((RectTransform)transform).rect.width - 40f);
            float height = _levelProgressText.GetPreferredValues(text, width, float.PositiveInfinity).y + 20f;
            _levelProgressPanel.sizeDelta = new Vector2(0f, height);
        }

        /// <summary>젤탕의 기존 두 칸을 드론 현황과 식량으로 바꾸고 다른 유닛에서 원래 아이콘을 복원한다.</summary>
        public void SetFoodStatMode(bool food)
        {
            if (_foodStatMode == food) return;
            if (_foodStatMode == null)
            {
                _firstStatIcon = FindStatIcon(statSlot_Atk);
                _secondStatIcon = FindStatIcon(statSlot_AtkSpeed);
                _attackIcon = _firstStatIcon != null ? _firstStatIcon.sprite : null;
                _speedIcon = _secondStatIcon != null ? _secondStatIcon.sprite : null;
                ConfigureStatText(statSlot_Atk);
                ConfigureStatText(statSlot_AtkSpeed);
            }
            _foodStatMode = food;
            if (_firstStatIcon != null) _firstStatIcon.sprite = food ? UnitStatType.DroneCount.GetVisual()?.icon : _attackIcon;
            if (_secondStatIcon != null) _secondStatIcon.sprite = food ? UnitStatType.FoodPerSecond.GetVisual()?.icon : _speedIcon;
        }

        private static void ConfigureStatText(UI_StatSlot slot)
        {
            if (slot == null) return;
            foreach (var text in slot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.fontSizeMax = Mathf.Max(40f, text.fontSize);
                text.fontSizeMin = Mathf.Max(28f, text.fontSizeMin);
                text.enableAutoSizing = true;
            }
        }

        private static Image FindStatIcon(UI_StatSlot slot)
        {
            if (slot == null) return null;
            foreach (var image in slot.GetComponentsInChildren<Image>(true))
                if (image.name == "Icon") return image;
            return null;
        }

        /// <summary>런타임 기본값과 초록색 강화 증가분을 표시한다. 공격 빈도는 초당 횟수다.</summary>
        public void UpdateStats(string atkValue, string atkSpeedValue)
        {
            if (_upgradeFeedback && _foodStatMode != true)
            {
                atkValue = $"{_previousAttack:0.##} → <color=#69C875>{atkValue}</color>";
                atkSpeedValue = $"{(_previousInterval > 0f ? 1f / _previousInterval : 0f):0.##}/초 → <color=#69C875>{atkSpeedValue}</color>";
            }
            if (statSlot_Atk != null)      statSlot_Atk.Setup(atkValue);
            if (statSlot_AtkSpeed != null) statSlot_AtkSpeed.Setup(atkSpeedValue);
        }
    }
}
