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
        [SerializeField] private bool useJump = false;
        [SerializeField] private float jumpAmount = 10f;
        [SerializeField] private Vector2 scaleMultiplier = new Vector2(1.05f, 0.95f);

        public override async UniTask Play()
        {
            Stop();

            float jump = useJump ? jumpAmount : 0;
            Vector3 targetSize = new Vector3(_originSize.x * scaleMultiplier.x, _originSize.y * scaleMultiplier.y, _originSize.z);

            _currentSeq = DOTween.Sequence()
                .Append(GetMoveTween(_originPos + new Vector3(0, jump, 0), duration).SetEase(ease))
                .Join(GetSizeTween(targetSize, duration).SetEase(ease))
                .Append(GetMoveTween(_originPos, duration).SetEase(ease))
                .Join(GetSizeTween(_originSize, duration).SetEase(ease))
                .SetDelay(delay);

            if (isLoop)
            {
                _currentSeq.SetLoops(-1);
            }

            await _currentSeq.Play().ToUniTask();
        }
    }
}
