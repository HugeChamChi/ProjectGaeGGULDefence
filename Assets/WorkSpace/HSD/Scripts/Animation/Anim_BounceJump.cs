using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 오브젝트가 튀어 올랐다 떨어지는 역동적인 효과를 줍니다.
    /// </summary>
    public class Anim_BounceJump : Anim_Base
    {
        [Header("Bounce Jump Settings")]
        [SerializeField] private float jumpPower = 150f;
        [SerializeField] private Vector2 squashMultiplier = new Vector2(1.3f, 0.7f);
        [SerializeField] private Vector2 stretchMultiplier = new Vector2(0.8f, 1.4f);

        public override async UniTask Play()
        {
            Stop();

            Vector3 squashScale = new Vector3(_originScale.x * squashMultiplier.x, _originScale.y * squashMultiplier.y, _originScale.z);
            Vector3 stretchScale = new Vector3(_originScale.x * stretchMultiplier.x, _originScale.y * stretchMultiplier.y, _originScale.z);
            Vector3 jumpPos = _originPos + new Vector3(0, jumpPower, 0);

            _currentSeq = DOTween.Sequence()
                .Append(GetScaleTween(squashScale, duration * 0.2f).SetEase(Ease.OutQuad))
                .Append(GetScaleTween(stretchScale, duration * 0.2f).SetEase(Ease.OutQuad))
                .Join(GetMoveTween(jumpPos, duration * 0.3f).SetEase(Ease.OutQuad))
                .Append(GetMoveTween(_originPos, duration * 0.3f).SetEase(Ease.InQuad))
                .Join(GetScaleTween(_originScale, duration * 0.3f).SetEase(Ease.OutBack))
                .Append(GetScaleTween(new Vector3(_originScale.x * 1.15f, _originScale.y * 0.85f, _originScale.z), 0.1f))
                .Append(GetScaleTween(_originScale, 0.1f))
                .SetDelay(delay);

            if (isLoop)
            {
                _currentSeq.SetLoops(-1);
            }

            await _currentSeq.Play().ToUniTask();
        }
    }
}
