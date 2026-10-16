using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HSD.InGameDebug
{
    public class UI_DebugItemSlot : MonoBehaviour
    {
        [SerializeField] private Image img_Icon;
        [SerializeField] private TextMeshProUGUI txt_Name;
        [SerializeField] private TextMeshProUGUI txt_Desc;
        [SerializeField] private Button btn_Action;
        [SerializeField] private TextMeshProUGUI txt_ActionBtn; // 'X' for remove, '+' for add, 'Apply' for Chief

        [Header("Button Sprites")]
        [SerializeField] private Sprite spr_Add;
        [SerializeField] private Sprite spr_Remove;

        private object _data;
        private Action<object> _onClickAction;
        private bool _modern;
        private string _searchText;

        /// <summary>Connects a generated row to the same action binding as authored legacy slots.</summary>
        public void Configure(Image icon, TextMeshProUGUI title, TextMeshProUGUI description, Button action, TMP_Text actionText)
        {
            img_Icon = icon; txt_Name = title; txt_Desc = description;
            btn_Action = action; txt_ActionBtn = (TextMeshProUGUI)actionText; _modern = true;
        }

        /// <summary>Matches a case-insensitive name, description or data identifier.</summary>
        public bool Matches(string query) => string.IsNullOrWhiteSpace(query) ||
            (_searchText != null && _searchText.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Updates availability while keeping the reason in the action label.</summary>
        public void SetActionEnabled(bool enabled) { if (btn_Action != null) btn_Action.interactable = enabled; }

        /// <summary>Binds a catalog or placed-object entry to its action.</summary>
        public void Init(object data, string name, string desc, Sprite icon, string btnText, Action<object> onClickAction, string searchKeywords = "")
        {
            _data = data;
            _searchText = name + " " + desc + " " + searchKeywords;
            
            if (txt_Name != null) txt_Name.text = name;
            if (txt_Desc != null) txt_Desc.text = desc;
            if (img_Icon != null)
            {
                img_Icon.sprite = icon;
                img_Icon.gameObject.SetActive(icon != null);
            }
            
            if (txt_ActionBtn != null)
            {
                txt_ActionBtn.text = btnText;
                // 기호일 경우 텍스트보다는 아이콘을 우선시함 (아이콘이 설정되어 있다면)
                bool isSymbol = btnText == "X" || btnText == "+";
                txt_ActionBtn.gameObject.SetActive(_modern || !isSymbol);
            }

            // 버튼 스프라이트 변경
            if (btn_Action != null)
            {
                var btnImg = btn_Action.GetComponent<Image>();
                if (btnImg != null)
                {
                    if (_modern)
                    {
                        bool remove = btnText == "제거";
                        bool inactive = btnText == "선택됨" || btnText == "획득함";
                        btnImg.color = remove ? new Color32(69, 43, 53, 255) : inactive ? DebugPanelLayout.Surface : DebugPanelLayout.Accent;
                        txt_ActionBtn.color = remove ? DebugPanelLayout.Danger : inactive ? DebugPanelLayout.Muted : DebugPanelLayout.Surface;
                    }
                    else if (btnText == "+" && spr_Add != null) btnImg.sprite = spr_Add;
                    else if (btnText == "X" && spr_Remove != null) btnImg.sprite = spr_Remove;
                }
            }

            _onClickAction = onClickAction;

            if (btn_Action != null)
            {
                btn_Action.interactable = true;
                btn_Action.onClick.RemoveAllListeners();
                btn_Action.onClick.AddListener(OnBtnClicked);
            }
        }

        private void OnBtnClicked()
        {
            _onClickAction?.Invoke(_data);
        }
    }
}
