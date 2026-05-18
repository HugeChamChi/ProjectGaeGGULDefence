using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 등장(In)과 퇴장(Out) 애니메이션을 모두 지원하는 베이스 클래스입니다.
    /// </summary>
    public abstract class Anim_InOutBase : Anim_Base
    {
        [Header("In Animation Settings")]
        [SerializeField] protected Ease easeIn = Ease.OutBack;
        [SerializeField] protected float durationIn = 0.3f;
        [SerializeField] protected float delayIn = 0f;

        [Header("Out Animation Settings")]
        [SerializeField] protected Ease easeOut = Ease.InBack;
        [SerializeField] protected float durationOut = 0.2f;
        [SerializeField] protected float delayOut = 0f;

        public override async UniTask Play()
        {
            await PlayIn();
        }

        [Button]
        public abstract UniTask PlayIn();

        [Button]
        public abstract UniTask PlayOut();

        [Button]
        public async UniTask PlayInOut()
        {
            await PlayIn();
            await PlayOut();
        }

        public override void Stop()
        {
            KillCurrentTween();
            ResetToOrigin();
        }
    }
}
