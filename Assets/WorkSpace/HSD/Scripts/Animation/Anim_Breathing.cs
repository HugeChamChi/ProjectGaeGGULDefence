using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 오브젝트가 숨을 쉬는 듯한 부드러운 반복 효과를 줍니다.
    /// </summary>
    public class Anim_Breathing : Anim_Base
    {
        [Header("Breathing Settings")]
        [SerializeField] private Vector2 scaleMultiplier = new Vector2(1.05f, 0.95f);

        public override async UniTask Play()
        {
            Stop();
            
            Vector3 targetScale = new Vector3(_originScale.x * scaleMultiplier.x, _originScale.y * scaleMultiplier.y, _originScale.z);

            _currentSeq = DOTween.Sequence()
                .Append(GetScaleTween(targetScale, duration).SetEase(ease))
                .Append(GetScaleTween(_originScale, duration).SetEase(ease))
                .SetDelay(delay);

            if (isLoop)
            {
                _currentSeq.SetLoops(-1);
            }

            await _currentSeq.Play().ToUniTask();
        }
    }
}
