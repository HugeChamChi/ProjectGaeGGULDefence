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
        [SerializeField] private Anim_Breathing loopEffect;     // 반복 효과 (Idle)
        [SerializeField] private Anim_BounceJump triggerEffect; // 트리거 효과 (Action)

        [Header("Settings")]
        [SerializeField] private bool playLoopAfterTrigger = true;
        private bool _isTriggerPlaying;

        private void Awake()
        {
            if (target == null) target = transform;

            // 컴포넌트 자동 감지 및 초기화
            if (loopEffect == null) loopEffect = GetComponent<Anim_Breathing>();
            if (triggerEffect == null) triggerEffect = GetComponent<Anim_BounceJump>();

            loopEffect?.Initialize(target);
            triggerEffect?.Initialize(target);
        }

        // Anim_Base에서 OnEnable을 처리하므로 여기서는 별도의 OnEnable/OnDisable 로직이 단순화됩니다.

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

            if (playLoopAfterTrigger) PlayLoop();
        }

        public void StopAll()
        {
            loopEffect?.Stop();
            triggerEffect?.Stop();
        }
    }
}