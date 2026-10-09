using GaeGGUL.Extension;
using VContainer;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 세팅 패널의 비즈니스 로직 Presenter
    /// </summary>
    public class UI_SettingPresenter
    {
        private AudioManager _audioManager;
        private GamePresentationSettings _settings;
        private SceneChangeManager _scenes;
        private bool _navigationStarted;

        private readonly UI_SettingPanel_Base _view;

        public UI_SettingPresenter(UI_SettingPanel_Base view, AudioManager audioManager)
        {
            _view = view;
            _audioManager = audioManager;
        }

        /// <summary>Awake에서 바인딩한 Presenter에 DI로 받은 서비스를 연결한다.</summary>
        public void Configure(AudioManager audioManager, GamePresentationSettings settings, SceneChangeManager scenes)
        {
            _audioManager = audioManager;
            _settings = settings;
            _scenes = scenes;
        }

        public void RefreshUI()
        {
            // AudioManager에서 현재 값 가져오기
            foreach (AudioGroup group in System.Enum.GetValues(typeof(AudioGroup)))
            {
                if (_audioManager == null) break;
                int vol = _audioManager.GetVolume(group);
                bool mute = _audioManager.IsMuted(group);
                _view.UpdateSoundSlot(group, vol, mute);
            }
            
            _view.UpdatePresentationToggles(_settings?.ShowDamageNumbers ?? true, _settings?.VibrationEnabled ?? true);
        }

        public void OnVolumeChanged(AudioGroup group, int value)
        {
            _audioManager.SetVolume(group, value);
        }

        public void OnMuteChanged(AudioGroup group, bool isMute)
        {
            _audioManager.SetMute(group, isMute);
        }

        public void OnLanguageClicked()
        {
            // TODO: 언어 변경 로직 구현
            Debug.Log("[TODO] Language Change Clicked");
        }

        public void OnDamageFloaterChanged(bool isOn)
        {
            _settings?.SetDamageNumbers(isOn);
            RefreshUI();
        }

        public void OnVibrationChanged(bool isOn)
        {
            _settings?.SetVibration(isOn);
            RefreshUI();
        }

        public void OnRestartClicked()
        {
            // View를 통해 로컬 확인 팝업 호출
            _view.ShowConfirm("다시 시작", $"진행중인 게임이 종료되며\n게임이 {"다시 시작".ToColor(Color.yellow)}됩니다.", () =>
            {
                NavigateAsync(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name).Forget();
            });
        }

        public void OnGoToLobbyClicked()
        {
            // View를 통해 로컬 확인 팝업 호출
            _view.ShowConfirm("로비로 이동", $"진행중인 게임이 종료되며\n{"로비".ToColor(Color.yellow)}로 돌아갑니다.", () =>
            {
                NavigateAsync("LobbyScene").Forget();
            });
        }

        private async UniTaskVoid NavigateAsync(string scene)
        {
            if (_navigationStarted) return;
            if (_scenes == null)
            {
                Debug.LogWarning("[UI_SettingPresenter] SceneChangeManager 미주입 — 씬 이동 불가");
                return;
            }
            _navigationStarted = true;
            try { await _scenes.TransitionToSceneAsync(scene); }
            catch (Exception error) { Debug.LogWarning("설정 창 씬 이동 실패: " + error.Message); }
            finally { _navigationStarted = false; }
        }
    }
}
