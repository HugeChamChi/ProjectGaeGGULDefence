using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>
    /// 튜토리얼 포커스 프레임 전용 펄스(숨쉬기) 애니메이션 스크립트입니다.
    /// 활성화(OnEnable) 시 자동으로 커졌다 작아지는 연출을 무한 반복합니다.
    /// </summary>
    public class UI_TutorialFocusAnim : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _scaleMultiplier = 1.05f;
        [SerializeField] private float _duration = 0.5f;

        private Tween _pulseTween;
        private Vector3 _originalScale;

        private void Awake()
        {
            // 초기 스케일 저장
            _originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            // 혹시 스케일이 꼬여있을 수 있으니 초기화
            transform.localScale = _originalScale;

            // Yoyo 형태로 커졌다 작아졌다 무한 반복
            _pulseTween = transform.DOScale(_originalScale * _scaleMultiplier, _duration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true); // 게임이 일시정지(Time.timeScale = 0) 되어도 애니메이션 재생
        }

        private void OnDisable()
        {
            // 비활성화 시 트윈 킬 및 스케일 원상 복구
            _pulseTween?.Kill();
            transform.localScale = _originalScale;
        }

        private void OnDestroy()
        {
            _pulseTween?.Kill();
        }
    }
}
