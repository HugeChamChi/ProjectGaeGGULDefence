using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 공통 설정을 담는 Base 클래스
    /// </summary>
    public class UI_SettingPanel_Base : UI_Base
    {
        [Header("Sound Slots")]
        [SerializeField] private List<UI_SoundSettingSlot> _soundSlots;

        [Header("Toggles & Buttons")]
        [SerializeField] protected Button btn_Language;
        [SerializeField] protected HSD.UI.Utils.UI_SlidingToggle toggle_DamageFloater;
        [SerializeField] protected HSD.UI.Utils.UI_SlidingToggle toggle_Vibration;

        [Header("Sub Popups")]
        [SerializeField] protected UI_Popup_Confirm confirmPopup;

        protected UI_SettingPresenter _presenter
        {
            get
            {
                if (_presenterValue == null)
                    _presenterValue = new UI_SettingPresenter(this);

                return _presenterValue;
            }

            set => _presenterValue = value;
        }
        private UI_SettingPresenter _presenterValue;

        protected override void Awake()
        {
            base.Awake();
            InitializeBase();
        }

        public void ShowConfirm(string title, string description, System.Action onYes, System.Action onNo = null)
        {
            if (confirmPopup != null)
            {
                confirmPopup.Show(title, description, onYes, onNo);
            }
            else
            {
                Debug.LogWarning("[UI_SettingPanel] ConfirmPopup reference is missing!");
            }
        }

        private void InitializeBase()
        {
            foreach (var slot in _soundSlots)
            {
                slot.Bind(_presenter.OnVolumeChanged, _presenter.OnMuteChanged);
            }

            if (btn_Language != null) btn_Language.onClick.AddListener(_presenter.OnLanguageClicked);

            // 슬라이딩 토글 초기화
            if (toggle_DamageFloater != null) toggle_DamageFloater.Init(true, _presenter.OnDamageFloaterChanged);
            if (toggle_Vibration != null) toggle_Vibration.Init(true, _presenter.OnVibrationChanged);

            BindCloseButton(btn_Close, btn_BackgroundClose);
        }

        public override async UniTask OpenAsync()
        {
            _presenter.RefreshUI();
            await base.OpenAsync();
        }

        public void UpdateSoundSlot(AudioGroup group, int volume, bool isMuted)
        {
            var slot = _soundSlots.Find(s => s.Group == group);
            if (slot != null)
            {
                slot.SetState(volume, isMuted);
            }
        }
    }
}
