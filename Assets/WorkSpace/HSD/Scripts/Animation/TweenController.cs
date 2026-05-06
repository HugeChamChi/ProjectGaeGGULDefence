using UnityEngine;
using Cysharp.Threading.Tasks;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 오브젝트의 여러 트윈 효과를 중앙 제어하는 컨트롤러입니다.
    /// </summary>
    public class TweenController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Effects")]
        [SerializeField] private BreathingEffect loopEffect;     // 반복 효과 (Idle)
        [SerializeField] private BounceJumpEffect triggerEffect; // 트리거 효과 (Action)

        [Header("Settings")]
        [SerializeField] private bool playLoopOnEnable = true;
        private bool _isTriggerPlaying;

        private void Awake()
        {
            if (target == null) target = transform;

            // 컴포넌트 자동 감지 및 초기화
            if (loopEffect == null) loopEffect = GetComponent<BreathingEffect>();
            if (triggerEffect == null) triggerEffect = GetComponent<BounceJumpEffect>();

            loopEffect?.Initialize(target);
            triggerEffect?.Initialize(target);
        }

        private void OnEnable()
        {
            if (playLoopOnEnable) PlayLoop();
        }

        private void OnDisable()
        {
            StopAll();
        }

        public void PlayLoop() => loopEffect?.Play().Forget();

        [Button("Play Trigger Effect")]
        public async UniTask PlayTrigger()
        {
            if (_isTriggerPlaying) return;
            _isTriggerPlaying = true;

            StopAll(); // 모든 트윈 중단 및 원본 복구
            
            if (triggerEffect != null)
                await triggerEffect.Play();

            _isTriggerPlaying = false;

            if (playLoopOnEnable) PlayLoop();
        }

        public void StopAll()
        {
            loopEffect?.Stop();
            triggerEffect?.Stop();
        }
    }
}