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

        public void Init(object data, string name, string desc, Sprite icon, string btnText, Action<object> onClickAction)
        {
            _data = data;
            
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
                txt_ActionBtn.gameObject.SetActive(!isSymbol);
            }

            // 버튼 스프라이트 변경
            if (btn_Action != null)
            {
                var btnImg = btn_Action.GetComponent<Image>();
                if (btnImg != null)
                {
                    if (btnText == "+" && spr_Add != null) btnImg.sprite = spr_Add;
                    else if (btnText == "X" && spr_Remove != null) btnImg.sprite = spr_Remove;
                }
            }

            _onClickAction = onClickAction;

            btn_Action.onClick.RemoveAllListeners();
            btn_Action.onClick.AddListener(OnBtnClicked);
        }

        private void OnBtnClicked()
        {
            _onClickAction?.Invoke(_data);
        }
    }
}