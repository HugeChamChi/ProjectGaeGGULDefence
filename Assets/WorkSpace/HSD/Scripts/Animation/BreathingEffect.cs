using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 오브젝트가 숨을 쉬는 듯한 부드러운 반복 효과를 줍니다.
    /// </summary>
    public class BreathingEffect : MonoBehaviour, ITweenEffect
    {
        [SerializeField] private bool useJump = false;
        [SerializeField] private float duration = 1.5f;
        [SerializeField] private float jumpAmount = 10f;
        [SerializeField] private Vector2 scaleMultiplier = new Vector2(1.05f, 0.95f);

        private Transform _target;
        private RectTransform _rect;
        private Vector2 _originSize;
        private Vector2 _originPos;
        private bool _isUI;
        private Sequence _sequence;

        public void Initialize(Transform target)
        {
            _target = target;
            _rect = target as RectTransform;
            _isUI = _rect != null;

            if (_isUI)
            {
                _originSize = _rect.sizeDelta;
                _originPos = _rect.anchoredPosition;
            }
            else
            {
                _originSize = target.localScale;
                _originPos = target.localPosition;
            }
        }

        public async UniTask Play()
        {
            Stop();
            _sequence = DOTween.Sequence();

            float jump = useJump ? jumpAmount : 0;
            
            _sequence.Append(GetMoveTween(_originPos + new Vector2(0, jump), duration).SetEase(Ease.InOutSine));
            _sequence.Join(GetSizeTween(_originSize * scaleMultiplier, duration).SetEase(Ease.InOutSine));
            
            _sequence.Append(GetMoveTween(_originPos, duration).SetEase(Ease.InOutSine));
            _sequence.Join(GetSizeTween(_originSize, duration).SetEase(Ease.InOutSine));

            _sequence.SetLoops(-1);
        }

        public void Stop()
        {
            if (_sequence != null)
            {
                _sequence.Kill();
                _sequence = null;
            }
            ResetToOrigin();
        }

        private void ResetToOrigin()
        {
            if (_target == null) return;
            _target.DOKill();

            if (_isUI)
            {
                _rect.anchoredPosition = _originPos;
                _rect.sizeDelta = _originSize;
            }
            else
            {
                _target.localPosition = _originPos;
                _target.localScale = _originSize;
            }
        }

        private Tweener GetMoveTween(Vector2 pos, float dur) => _isUI ? _rect.DOAnchorPos(pos, dur) : _target.DOLocalMove(pos, dur);
        private Tweener GetSizeTween(Vector2 size, float dur) => _isUI ? _rect.DOSizeDelta(size, dur) : _target.DOScale(size, dur);
    }
}