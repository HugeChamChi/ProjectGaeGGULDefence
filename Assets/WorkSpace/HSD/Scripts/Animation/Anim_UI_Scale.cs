using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// UI의 localScale을 조절하는 양방향(In/Out) 스케일 애니메이션입니다.
    /// </summary>
    public class Anim_UI_Scale : Anim_InOutBase
    {
        [Header("Scale Multiplier")]
        [SerializeField] private Vector2 startMultiplier = Vector2.zero;
        [SerializeField] private Vector2 endMultiplier = Vector2.zero;

        public override async UniTask PlayIn()
        {
            KillCurrentTween();

            // 1. 시작 크기 설정 (localScale 기준)
            _target.localScale = new Vector3(_originScale.x * startMultiplier.x, _originScale.y * startMultiplier.y, _originScale.z);

            // 2. 원래 크기로 커지는 애니메이션
            _currentSeq = DOTween.Sequence()
                .SetUpdate(ignoreTimeScale)
                .Append(GetScaleTween(_originScale, durationIn).SetEase(easeIn))
                .SetDelay(delayIn);

            await _currentSeq.Play().ToUniTask();
        }

        public override async UniTask PlayOut()
        {
            KillCurrentTween();

            // 1. 목표 크기 설정
            Vector3 endScale = new Vector3(_originScale.x * endMultiplier.x, _originScale.y * endMultiplier.y, _originScale.z);

            // 2. 목표 크기로 작아지는 애니메이션
            _currentSeq = DOTween.Sequence()
                .SetUpdate(ignoreTimeScale)
                .Append(GetScaleTween(endScale, durationOut).SetEase(easeOut))
                .SetDelay(delayOut);

            await _currentSeq.Play().ToUniTask();
        }
    }
}
