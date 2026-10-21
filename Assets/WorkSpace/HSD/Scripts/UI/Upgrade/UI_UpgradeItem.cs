using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HSD.UI.Upgrade
{
    /// <summary>
    /// 강화 카드 한 장. 카드 어디를 눌러도(배경·아이콘·텍스트) 강화를 요청한다 — 자식 그래픽의 클릭이 이 루트로 올라온다.
    /// 결과 연출은 UI_UpgradeItemFeedback이 담당한다.
    /// </summary>
    public class UI_UpgradeItem : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image img_Icon;
        [SerializeField] private TextMeshProUGUI txt_Name;
        [SerializeField] private TextMeshProUGUI txt_Level;
        [SerializeField] private TextMeshProUGUI txt_Cost;
        [SerializeField] private Button btn_Upgrade;
        [SerializeField] private Image img_CostIcon;
        [Header("Upgrade Button States")]
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _pressedSprite;
        private TextMeshProUGUI _description;

        private string _target;
        private Action<string> _onUpgradeClicked;
        private bool _canUpgrade;
        private float _currentCost;
        private float _requestedCost;
        private UI_UpgradeItemFeedback _feedback;

        public bool CanUpgrade => _canUpgrade;
        public string UpgradeTarget => _target;
        public float CurrentCost => _currentCost;
        public int CurrentLevel { get; private set; }

        private void Awake()
        {
            ConfigureTextAreas();
            if (btn_Upgrade != null) btn_Upgrade.onClick.AddListener(RequestUpgrade);
            _feedback = GetComponent<UI_UpgradeItemFeedback>();
            if (_feedback == null) _feedback = gameObject.AddComponent<UI_UpgradeItemFeedback>();
            _feedback.Bind(transform as RectTransform, txt_Level, txt_Cost, img_Icon != null ? img_Icon.rectTransform : null);
        }

        private void ConfigureTextAreas()
        {
            if (img_Icon != null && img_Icon.transform.parent is RectTransform iconSlot)
            {
                iconSlot.anchorMin = new Vector2(0.1f, 0.65f);
                iconSlot.anchorMax = new Vector2(0.9f, 0.98f);
                iconSlot.offsetMin = iconSlot.offsetMax = Vector2.zero;
            }
            if (txt_Level != null)
            {
                txt_Level.enableAutoSizing = true;
                txt_Level.fontSizeMin = UiTypography.AutoSizeMinimum;
                txt_Level.fontSizeMax = Mathf.Max(UiTypography.BodySize, txt_Level.fontSize);
                if (txt_Level.transform.parent is RectTransform badge)
                {
                    badge.sizeDelta = new Vector2(130f, 70f);
                    badge.anchoredPosition = new Vector2(60f, 20f);
                }
            }
            if (txt_Cost != null)
            {
                txt_Cost.rectTransform.sizeDelta = new Vector2(120f, 0f);
                txt_Cost.enableAutoSizing = true;
                txt_Cost.fontSizeMin = UiTypography.AutoSizeMinimum;
                txt_Cost.fontSizeMax = Mathf.Max(UiTypography.NumericSize, txt_Cost.fontSize);
                txt_Cost.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        /// <summary>버튼 밖(카드 배경·아이콘·텍스트)을 눌렀을 때. 버튼 위 클릭은 버튼이 먼저 처리한다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            RequestUpgrade();
        }

        private void RequestUpgrade()
        {
            if (!_canUpgrade) return;
            // 강화 성공 시 UpdateUI가 다음 비용으로 덮어쓰므로, 이번에 쓴 비용을 먼저 기억한다.
            _requestedCost = _currentCost;
            _onUpgradeClicked?.Invoke(_target);
        }

        /// <summary>강화 성공 연출 (이번에 쓴 비용 표시 포함).</summary>
        public void PlayUpgradeSuccess() => _feedback?.PlaySuccess(_requestedCost);

        /// <summary>강화 실패 연출 (재화 부족 등).</summary>
        public void PlayUpgradeRejected() => _feedback?.PlayRejected();

        public void Init(UpgradeModel.UpgradeItemData data, Action<string> onUpgradeClicked)
        {
            _target = data.UpgradeTarget;
            _onUpgradeClicked = onUpgradeClicked;
            
            UpdateUI(data);
        }

        public void UpdateUI(UpgradeModel.UpgradeItemData data)
        {
            CurrentLevel = data.CurrentLevel;
            _canUpgrade = data.UpgradeCost >= 0f;
            _currentCost = data.UpgradeCost;
            if (txt_Name != null) txt_Name.gameObject.SetActive(false);
            if (txt_Level != null) txt_Level.text = $"Lv.{data.CurrentLevel}";
            if (txt_Cost != null) txt_Cost.text = data.UpgradeCost >= 0f ? FormatCost(data.UpgradeCost) : "—";
            if (img_CostIcon != null) img_CostIcon.gameObject.SetActive(data.UpgradeCost >= 0f);
            UpdateButton();
            EnsureDescription();
            if (_description != null) _description.text = data.Description;

            if (img_Icon != null && data.Icon != null)
            {
                img_Icon.sprite = data.Icon;
            }
        }

        public void SetUpgradeInteractable(bool interactable)
        {
            if (btn_Upgrade != null)
            {
                btn_Upgrade.interactable = interactable && _canUpgrade;
            }
        }

        /// <summary>큰 비용도 정수 오버플로 없이 카드에 들어가는 길이로 표시한다.</summary>
        public static string FormatCost(float cost) => cost < 1000000f ? cost.ToString("N0") : cost.ToString("0.###E+0");

        private void EnsureDescription()
        {
            if (_description != null || txt_Level == null) return;
            _description = Instantiate(txt_Level, transform, false);
            _description.name = "UpgradeDescription";
            _description.raycastTarget = false;
            _description.richText = true;
            _description.fontSizeMin = 28f;
            _description.fontSizeMax = 32f;
            _description.enableAutoSizing = true;
            _description.alignment = TextAlignmentOptions.Center;
            var layout = _description.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var rect = _description.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.13f);
            rect.anchorMax = new Vector2(1f, 0.60f);
            rect.offsetMin = new Vector2(-12f, 0f);
            rect.offsetMax = new Vector2(12f, 0f);
        }

        private void UpdateButton()
        {
            if (btn_Upgrade == null) return;
            btn_Upgrade.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            var state = btn_Upgrade.spriteState;
            state.highlightedSprite = _normalSprite;
            state.selectedSprite = _normalSprite;
            state.pressedSprite = _pressedSprite;
            state.disabledSprite = _normalSprite;
            btn_Upgrade.spriteState = state;
            if (btn_Upgrade.targetGraphic is Image image)
            {
                image.sprite = _normalSprite;
                image.overrideSprite = null;
            }
            btn_Upgrade.interactable = _canUpgrade;
        }
    }
}
