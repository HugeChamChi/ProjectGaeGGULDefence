using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System;

namespace HSD.UI.Utils
{
    /// <summary>
    /// Slider 컴포넌트를 활용하여 유지보수성과 레이아웃 유연성을 확보한 애니메이션 토글
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class UI_SlidingToggle : MonoBehaviour, IPointerClickHandler
    {
        private Slider _slider;

        [Header("Background Objects")]
        [SerializeField] private GameObject backgroundOn;
        [SerializeField] private GameObject backgroundOff;

        [Header("Animation Settings")]
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private Ease easeType = Ease.OutQuart;

        private bool _isOn;
        private Action<bool> _onValueChanged;

        public bool IsOn => _isOn;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            
            // Slider 기본 설정 강제: 코드 레벨에서 실수 방지 (Defensive Programming)
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.wholeNumbers = false;
            _slider.interactable = false; // 기본적으로 클릭/드래그는 이 스크립트에서 제어
            
            // Transition이 None이 아니면 예기치 못한 색상 변경이 일어날 수 있음
            _slider.transition = Selectable.Transition.None;
        }

        /// <summary>
        /// 초기 상태 및 콜백 설정
        /// </summary>
        public void Init(bool startState, Action<bool> onValueChanged)
        {
            _isOn = startState;
            _onValueChanged = onValueChanged;

            // 초기화 시에는 애니메이션 없이 즉시 반영
            UpdateVisual(true);
        }

        /// <summary>
        /// IPointerClickHandler 구현: 영역 어디를 클릭해도 토글 동작
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            Toggle();
        }

        public void Toggle()
        {
            _isOn = !_isOn;
            _onValueChanged?.Invoke(_isOn);
            UpdateVisual(false);
        }

        public void SetState(bool isOn, bool immediate = false)
        {
            if (_isOn == isOn) return;
            
            _isOn = isOn;
            UpdateVisual(immediate);
        }

        private void UpdateVisual(bool immediate)
        {
            float targetValue = _isOn ? 1f : 0f;

            // 1. 배경 오브젝트 교체 (상태 즉시 반영)
            if (backgroundOn != null) backgroundOn.SetActive(_isOn);
            if (backgroundOff != null) backgroundOff.SetActive(!_isOn);

            // 2. Slider Value 애니메이션 (핸들 이동)
            if (_slider != null)
            {
                _slider.DOKill(); // 기존 트윈 제거 (안전성 확보)
                
                if (immediate)
                {
                    _slider.value = targetValue;
                }
                else
                {
                    // 엔진의 Slider.value를 직접 애니메이션하여 레이아웃 유연성 확보
                    _slider.DOValue(targetValue, duration)
                           .SetEase(easeType)
                           .SetUpdate(true); // TimeScale 독립성 확보 (일시정지 중에도 동작 가능하도록)
                }
            }
        }
        
        private void OnDestroy()
        {
            _slider?.DOKill();
        }
    }
}
