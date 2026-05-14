using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 지정된 오프셋에서 들어오고 나가는 양방향(In/Out) 슬라이드 애니메이션입니다.
    /// </summary>
    public class Anim_UI_Slide : Anim_InOutBase
    {
        [Header("Slide Offset")]
        [SerializeField] private Vector3 inOffset;
        [SerializeField] private Vector3 outOffset;

        public override async UniTask PlayIn()
        {
            KillCurrentTween();

            // 1. 시작 위치 설정
            Vector3 startPos = _originPos + inOffset;
            
            if (_isUI) _rect.anchoredPosition = startPos;
            else _target.localPosition = startPos;

            // 2. 원래 위치로 이동
            _currentSeq = DOTween.Sequence()
                .Append(GetMoveTween(_originPos, durationIn).SetEase(easeIn))
                .SetDelay(delayIn);

            await _currentSeq.Play().ToUniTask();
        }

        public override async UniTask PlayOut()
        {
            KillCurrentTween();

            // 1. 목표 위치 설정
            Vector3 endPos = _originPos + outOffset;

            // 2. 목표 위치로 이동
            _currentSeq = DOTween.Sequence()
                .Append(GetMoveTween(endPos, durationOut).SetEase(easeOut))
                .SetDelay(delayOut);

            await _currentSeq.Play().ToUniTask();
        }
    }
}
