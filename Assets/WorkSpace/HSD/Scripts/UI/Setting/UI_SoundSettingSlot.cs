using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 개별 사운드 설정 항목 (슬라이더 + 뮤트 토글) 슬롯
    /// </summary>
    public class UI_SoundSettingSlot : MonoBehaviour
    {
        [SerializeField] private AudioGroup _group;
        [SerializeField] private Slider _slider;
        [SerializeField] private Toggle _muteToggle;
        
        [Header("Visual Objects")]
        [SerializeField] private GameObject _objSoundOn;
        [SerializeField] private GameObject _objSoundOff;
        [SerializeField, Min(0f)] private float _transitionSeconds = 0.2f;

        private CanvasGroup _soundOnGroup, _soundOffGroup;
        private Tween _visualTween;
        private bool _hasVisualState, _soundOn;
        private float _visualProgress;

        public AudioGroup Group => _group;

        private Action<AudioGroup, int> _onVolumeChanged;
        private Action<AudioGroup, bool> _onMuteChanged;

        public void Bind(Action<AudioGroup, int> onVolumeChanged, Action<AudioGroup, bool> onMuteChanged)
        {
            _onVolumeChanged = onVolumeChanged;
            _onMuteChanged = onMuteChanged;

            _slider.onValueChanged.RemoveAllListeners();
            _slider.onValueChanged.AddListener(val => 
            {
                // Slider가 0~1 범위면 100을 곱해서 전달, 0~100 범위면 그대로 전달
                // 시니어 팁: UI 컴포넌트의 설정값에 유연하게 대응하도록 설계
                int volume = _slider.maxValue <= 1.01f ? (int)(val * 100) : (int)val;
                _onVolumeChanged?.Invoke(_group, volume);
            });

            _muteToggle.onValueChanged.RemoveAllListeners();
            _muteToggle.onValueChanged.AddListener(isOn => 
            {
                UpdateVisual(isOn);
                _onMuteChanged?.Invoke(_group, !isOn); // Toggle IsOn이 true면 소리가 켜진 것(Mute=false)
            });
        }

        public void SetState(int volume, bool isMuted)
        {
            // Slider 범위에 맞춰 값 보정하여 설정 (정수 나눗셈 방지를 위해 100f 사용)
            _slider.SetValueWithoutNotify(_slider.maxValue <= 1.01f ? volume / 100f : volume);
            _muteToggle.SetIsOnWithoutNotify(!isMuted);
            UpdateVisual(!isMuted);
        }

        private void UpdateVisual(bool isSoundOn)
        {
            if (_hasVisualState && _soundOn == isSoundOn) return;
            if (_soundOnGroup == null && _objSoundOn != null)
            {
                _soundOnGroup = _objSoundOn.GetComponent<CanvasGroup>();
                if (_soundOnGroup == null) _soundOnGroup = _objSoundOn.AddComponent<CanvasGroup>();
            }
            if (_soundOffGroup == null && _objSoundOff != null)
            {
                _soundOffGroup = _objSoundOff.GetComponent<CanvasGroup>();
                if (_soundOffGroup == null) _soundOffGroup = _objSoundOff.AddComponent<CanvasGroup>();
            }
            bool immediate = !_hasVisualState || !Application.isPlaying || !isActiveAndEnabled || _transitionSeconds <= 0f;
            _hasVisualState = true;
            _soundOn = isSoundOn;
            _visualTween?.Kill();
            _visualTween = null;
            float target = isSoundOn ? 1f : 0f;
            if (immediate)
            {
                _visualProgress = target;
                ApplyVisual();
            }
            else
            {
                _visualTween = DOTween.To(() => _visualProgress, value => { _visualProgress = value; ApplyVisual(); }, target, _transitionSeconds)
                    .SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject)
                    .OnComplete(() => _visualTween = null);
            }
        }

        private void ApplyVisual()
        {
            if (_soundOnGroup != null)
            {
                _soundOnGroup.gameObject.SetActive(_visualProgress > 0f || _soundOn);
                _soundOnGroup.alpha = _visualProgress;
            }
            if (_soundOffGroup != null)
            {
                _soundOffGroup.gameObject.SetActive(_visualProgress < 1f || !_soundOn);
                _soundOffGroup.alpha = 1f - _visualProgress;
            }
        }

        private void OnEnable() => _hasVisualState = false;

        private void OnDisable()
        {
            _visualTween?.Kill();
            _visualTween = null;
            _hasVisualState = false;
        }

        private void OnDestroy() => _visualTween?.Kill();
    }
}
