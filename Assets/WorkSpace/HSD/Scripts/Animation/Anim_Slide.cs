using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 설정한 오프셋 위치에서 원래 위치로 슬라이드하며 이동하는 애니메이션입니다.
    /// 현재 위치가 최종 목적지가 됩니다.
    /// </summary>
    public class Anim_Slide : Anim_Base
    {
        [Header("Slide Settings")]
        [SerializeField] private Vector3 offset;

        public override async UniTask Play()
        {
            Stop();

            // 1. 시작 위치 설정 (원래 위치 + 오프셋)
            Vector3 startPos = _originPos + offset;
            
            if (_isUI)
            {
                _rect.anchoredPosition = startPos;
            }
            else
            {
                _target.localPosition = startPos;
            }

            // 2. 원래 위치(_originPos)로 이동하는 시퀀스 구성
            _currentSeq = DOTween.Sequence()
                .Append(GetMoveTween(_originPos, duration).SetEase(ease))
                .SetDelay(delay);

            if (isLoop)
            {
                _currentSeq.SetLoops(-1, LoopType.Restart);
            }

            await _currentSeq.Play().ToUniTask();
        }
    }
}
