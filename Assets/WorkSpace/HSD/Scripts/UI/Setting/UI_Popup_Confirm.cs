using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using GaeGGUL.Animation;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 제목과 설명, 확인/취소 콜백을 제어하는 공통 팝업 패널
    /// UI_Base를 상속받아 통일된 Open/Close 인터페이스를 제공합니다.
    /// </summary>
    public class UI_Popup_Confirm : UI_Base
    {
        [Header("Content Elements")]
        [SerializeField] private TMP_Text txt_Title;
        [SerializeField] private TMP_Text txt_Description;

        [Header("Interaction Buttons")]
        [SerializeField] private Button btn_Yes;
        [SerializeField] private Button btn_No;

        [Header("Animation")]
        private Action _onYes;
        private Action _onNo;

        protected override void Awake()
        {
            base.Awake();

            // 취소(No) 버튼은 기본적으로 팝업을 닫도록 설정
            if (btn_No != null)
            {
                btn_No.onClick.AddListener(() => 
                {
                    _onNo?.Invoke();
                    Close();
                });
            }

            // 확인(Yes) 버튼 리스너 등록
            if (btn_Yes != null)
            {
                btn_Yes.onClick.AddListener(() => 
                {
                    _onYes?.Invoke();
                    Close();
                });
            }

            // 배경 클릭 시에도 닫히도록 바인딩
            BindCloseButton(btn_BackgroundClose);
        }

        /// <summary>
        /// 팝업의 내용을 설정하고 오픈합니다.
        /// </summary>
        public void Show(string title, string description, Action onYes, Action onNo = null)
        {
            txt_Title.text = title;
            txt_Description.text = description;
            _onYes = onYes;
            _onNo = onNo;

            Open();
        }
    }
}
