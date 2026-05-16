using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 설정한 배율(Scale)에서 원래 크기로 조절하며 재생하는 애니메이션입니다.
    /// localScale을 조절하여 앵커 설정과 무관하게 일관된 스케일링을 제공합니다.
    /// </summary>
    public class Anim_Scale : Anim_Base
    {
        [Header("Scale Settings")]
        [SerializeField] private Vector3 scaleMultiplier = Vector3.zero;

        public override async UniTask Play()
        {
            Stop();

            // 1. 시작 크기 설정 (원래 배율 * 배율)
            Vector3 startScale = new Vector3(
                _originScale.x * scaleMultiplier.x,
                _originScale.y * scaleMultiplier.y,
                _originScale.z * scaleMultiplier.z
            );
            
            _target.localScale = startScale;

            // 2. 원래 배율(_originScale)로 복구되는 시퀀스 구성
            _currentSeq = DOTween.Sequence()
                .SetUpdate(ignoreTimeScale)
                .Append(GetScaleTween(_originScale, duration).SetEase(ease))
                .SetDelay(delay);

            if (isLoop)
            {
                _currentSeq.SetLoops(-1, LoopType.Restart);
            }

            await _currentSeq.Play().ToUniTask();
        }
    }
}
