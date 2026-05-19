using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HSD.InGameDebug
{
    /// <summary>
    /// Image 컴포넌트가 있는 오브젝트에 부착하여 특정 횟수 클릭 시 대상 GameObject를 활성화합니다.
    /// 디버그 UI 진입용 히든 버튼 등에 사용됩니다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class HiddenDebugActivator : MonoBehaviour, IPointerClickHandler
    {
        [Header("Settings")]
        [SerializeField] private GameObject targetObject;
        [SerializeField] private int requiredClickCount = 4;
        [SerializeField] private float resetTime = 2f; // 일정 시간 동안 클릭이 없으면 카운트 초기화

        private int _currentClickCount = 0;
        private float _lastClickTime = 0f;

        public void OnPointerClick(PointerEventData eventData)
        {
            float currentTime = Time.unscaledTime;

            // 마지막 클릭으로부터 시간이 너무 많이 지났으면 카운트 초기화
            if (currentTime - _lastClickTime > resetTime)
            {
                _currentClickCount = 0;
            }

            _lastClickTime = currentTime;
            _currentClickCount++;

            if (_currentClickCount >= requiredClickCount)
            {
                ActivateTarget();
                _currentClickCount = 0; // 활성화 후 카운트 초기화
            }
        }

        private void ActivateTarget()
        {
            if (targetObject != null)
            {
                targetObject.SetActive(true);
                
                // UI_Base를 상속받은 경우 Open() 메서드 호출 시도
                var uiBase = targetObject.GetComponent<UI_Base>();
                if (uiBase != null)
                {
                    uiBase.Open();
                }

                Debug.Log($"[HiddenDebugActivator] Target '{targetObject.name}' activated after {requiredClickCount} clicks.");
            }
        }
    }
}