using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 공통 설정을 담는 Base 클래스
    /// </summary>
    public class UI_SettingPanel_Base : UI_Base, ISettingsView
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
                    _presenterValue = new UI_SettingPresenter(this, _settingsAudioManager);

                return _presenterValue;
            }

            set => _presenterValue = value;
        }
        private UI_SettingPresenter _presenterValue;
        private AudioManager _settingsAudioManager;
        private GamePresentationSettings _presentationSettings;
        private GGD.UI.Authoring.AuthoringAssets _authoredAssets;
        private GGD.UI.Authoring.AuthoredUiView _authoredView;
        private SettingsGameBindings _authoredBindings;
        private bool _viewInitialized;

        /// <summary>Whether this host uses the installed source-authored screen.</summary>
        public bool UsesAuthoredUi => _authoredAssets != null;
        /// <summary>Current authored graph for targeted integration checks.</summary>
        public GGD.UI.Authoring.AuthoredUiView AuthoredView => _authoredView;
        /// <summary>Actual service adapter for targeted integration checks.</summary>
        public SettingsGameBindings AuthoredBindings => _authoredBindings;

        [VContainer.Inject]
        public void Construct(AudioManager audioManager, GamePresentationSettings settings, SceneChangeManager scenes)
        {
            _settingsAudioManager = audioManager;
            _presentationSettings = settings;
            _presenter.Configure(audioManager, settings, scenes);
        }

        protected override void Awake()
        {
            InitializeView();
        }

        private void InitializeView()
        {
            if (_viewInitialized) return;
            _viewInitialized = true;
            base.Awake();
            _authoredAssets = Resources.Load<GGD.UI.Authoring.AuthoringAssets>("UiAuthoring/Settings");
            if (_authoredAssets == null) InitializeBase();
            else
            {
                btn_Close?.onClick.RemoveAllListeners(); btn_BackgroundClose?.onClick.RemoveAllListeners();
                btn_Close = null; btn_BackgroundClose = null;
                foreach (Transform child in transform)
                    if (confirmPopup == null || child != confirmPopup.transform) child.gameObject.SetActive(false);
            }
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
            // Serialized popup roots start inactive. Their first OpenAsync can precede Unity's Awake.
            InitializeView();
            if (UsesAuthoredUi)
            {
                EnsureAuthoredView();
                _authoredBindings.OnOpen();
            }
            _presenter.RefreshUI();
            await base.OpenAsync();
        }

        private void EnsureAuthoredView()
        {
            if (_authoredView != null) return;
            var resolved = _authoredAssets.Resolve("Screens/UI3030.xml");
            var bindings = new SettingsGameBindings(_settingsAudioManager, _presentationSettings, _presenter, this, GetComponentInParent<LobbySettingsPopup>(true) == null);
            try
            {
                var candidate = GGD.UI.Authoring.AuthoredUiView.Create(resolved, (RectTransform)transform,
                    new GGD.UI.Authoring.AuthoringResources(_authoredAssets), bindings);
                _authoredView = candidate; _authoredBindings = bindings;
                string animationId = (string)resolved.Root.Attribute("animation");
                var window = animationId != null && candidate.Elements.TryGetValue(animationId, out var animationRect) ? animationRect : candidate.Elements.Values.FirstOrDefault(x => x.name == "Window");
                if (targetAnim != null && window != null) targetAnim.Initialize(window);
                candidate.Root.SetActive(true);
                confirmPopup?.transform.SetAsLastSibling();
            }
            catch { bindings.Dispose(); throw; }
        }

        public void UpdateSoundSlot(AudioGroup group, int volume, bool isMuted)
        {
            if (UsesAuthoredUi) { _authoredView?.Refresh(); return; }
            var slot = _soundSlots.Find(s => s.Group == group);
            if (slot != null)
            {
                slot.SetState(volume, isMuted);
            }
        }

        /// <summary>콜백을 다시 실행하지 않고 저장된 토글 상태를 표시한다.</summary>
        public void UpdatePresentationToggles(bool damageNumbers, bool vibration)
        {
            if (UsesAuthoredUi) { _authoredView?.Refresh(); return; }
            toggle_DamageFloater?.SetState(damageNumbers, true);
            toggle_Vibration?.SetState(vibration, true);
        }

        private void OnDestroy() { _authoredView?.Dispose(); _authoredBindings?.Dispose(); }
    }
}
