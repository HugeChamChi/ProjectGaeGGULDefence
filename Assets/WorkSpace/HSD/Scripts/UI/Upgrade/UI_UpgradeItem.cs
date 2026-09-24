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
        [SerializeField] private Sprite _maxSprite;

        private string _target;
        private Action<string> _onUpgradeClicked;
        private bool _canUpgrade;
        private int _currentCost;
        private int _requestedCost;
        private UI_UpgradeItemFeedback _feedback;

        private void Awake()
        {
            if (btn_Upgrade != null) btn_Upgrade.onClick.AddListener(RequestUpgrade);
            _feedback = GetComponent<UI_UpgradeItemFeedback>();
            if (_feedback == null) _feedback = gameObject.AddComponent<UI_UpgradeItemFeedback>();
            _feedback.Bind(transform as RectTransform, txt_Level, txt_Cost, img_Icon != null ? img_Icon.rectTransform : null);
        }

        /// <summary>버튼 밖(카드 배경·아이콘·텍스트)을 눌렀을 때. 버튼 위 클릭은 버튼이 먼저 처리한다.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            RequestUpgrade();
        }

        private void RequestUpgrade()
        {
            if (!_canUpgrade) return; // 최대 레벨
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
            _canUpgrade = !data.IsMaxLevel && data.UpgradeCost >= 0;
            _currentCost = data.UpgradeCost;
            if (txt_Name != null) txt_Name.text = data.DisplayName;
            
            if (data.IsMaxLevel)
            {
                if (txt_Level != null) txt_Level.text = $"Lv.{data.CurrentLevel}";
                if (txt_Cost != null) txt_Cost.text = "MAX";
                if (btn_Upgrade != null) btn_Upgrade.interactable = false;
            }
            else
            {
                if (txt_Level != null) txt_Level.text = $"Lv.{data.CurrentLevel}";
                if (txt_Cost != null) txt_Cost.text = data.UpgradeCost >= 0 ? data.UpgradeCost.ToString("N0") : "—";
            }
            if (img_CostIcon != null) img_CostIcon.gameObject.SetActive(!data.IsMaxLevel);
            UpdateButton(data.IsMaxLevel);

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

        private void UpdateButton(bool isMax)
        {
            if (btn_Upgrade == null) return;
            btn_Upgrade.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            var state = btn_Upgrade.spriteState;
            state.highlightedSprite = _normalSprite;
            state.selectedSprite = _normalSprite;
            state.pressedSprite = _pressedSprite;
            state.disabledSprite = isMax ? _maxSprite : _normalSprite;
            btn_Upgrade.spriteState = state;
            if (btn_Upgrade.targetGraphic is Image image)
            {
                image.sprite = isMax ? _maxSprite : _normalSprite;
                image.overrideSprite = null;
            }
            btn_Upgrade.interactable = _canUpgrade;
        }
    }
}
