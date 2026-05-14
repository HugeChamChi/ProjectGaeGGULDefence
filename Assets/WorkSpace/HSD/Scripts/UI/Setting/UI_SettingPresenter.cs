using GaeGGUL.Extension;
using UnityEngine;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 세팅 패널의 비즈니스 로직 Presenter
    /// </summary>
    public class UI_SettingPresenter
    {
        private readonly UI_SettingPanel_Base _view;

        public UI_SettingPresenter(UI_SettingPanel_Base view)
        {
            _view = view;
        }

        public void RefreshUI()
        {
            // AudioManager에서 현재 값 가져오기
            foreach (AudioGroup group in System.Enum.GetValues(typeof(AudioGroup)))
            {
                int vol = AudioManager.Instance.GetVolume(group);
                bool mute = AudioManager.Instance.IsMuted(group);
                _view.UpdateSoundSlot(group, vol, mute);
            }
            
            // TODO: 저장된 세팅 값(언어, 진동 등) 로드하여 View 업데이트
        }

        public void OnVolumeChanged(AudioGroup group, int value)
        {
            AudioManager.Instance.SetVolume(group, value);
        }

        public void OnMuteChanged(AudioGroup group, bool isMute)
        {
            AudioManager.Instance.SetMute(group, isMute);
        }

        public void OnLanguageClicked()
        {
            // TODO: 언어 변경 로직 구현
            Debug.Log("[TODO] Language Change Clicked");
        }

        public void OnDamageFloaterChanged(bool isOn)
        {
            // TODO: 데미지 플로터 On/Off 로직 구현
            Debug.Log($"[TODO] Damage Floater Changed: {isOn}");
        }

        public void OnVibrationChanged(bool isOn)
        {
            // TODO: 진동 On/Off 로직 구현
            Debug.Log($"[TODO] Vibration Changed: {isOn}");
        }

        public void OnRestartClicked()
        {
            // View를 통해 로컬 확인 팝업 호출
            _view.ShowConfirm("다시 시작", $"진행중인 게임이 종료되며\n게임이 {"다시 시작".ToColor(Color.yellow)}됩니다.", () =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            });
        }

        public void OnGoToLobbyClicked()
        {
            // View를 통해 로컬 확인 팝업 호출
            _view.ShowConfirm("로비로 이동", $"진행중인 게임이 종료되며\n{"로비".ToColor(Color.yellow)}로 돌아갑니다.", () =>
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
            });
        }
    }
}
