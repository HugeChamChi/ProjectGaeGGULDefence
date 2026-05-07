using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 오브젝트가 튀어 올랐다 떨어지는 역동적인 효과를 줍니다.
    /// </summary>
    public class BounceJumpEffect : MonoBehaviour, ITweenEffect
    {
        [SerializeField] private float jumpPower = 150f;
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private Vector2 squashMultiplier = new Vector2(1.3f, 0.7f);
        [SerializeField] private Vector2 stretchMultiplier = new Vector2(0.8f, 1.4f);

        private Transform _target;
        private RectTransform _rect;
        private Vector2 _originSize;
        private Vector2 _originPos;
        private bool _isUI;

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
            _target.DOKill();

            Sequence seq = DOTween.Sequence()
                .Append(GetSizeTween(_originSize * squashMultiplier, duration * 0.2f).SetEase(Ease.OutQuad))
                .Append(GetSizeTween(_originSize * stretchMultiplier, duration * 0.2f).SetEase(Ease.OutQuad))
                .Join(GetMoveTween(_originPos + new Vector2(0, jumpPower), duration * 0.3f).SetEase(Ease.OutQuad))
                .Append(GetMoveTween(_originPos, duration * 0.3f).SetEase(Ease.InQuad))
                .Join(GetSizeTween(_originSize, duration * 0.3f).SetEase(Ease.OutBack))
                .Append(GetSizeTween(_originSize * new Vector2(1.15f, 0.85f), 0.1f))
                .Append(GetSizeTween(_originSize, 0.1f));

            await seq.Play().ToUniTask();
        }

        public void Stop()
        {
            if (_target != null) _target.DOKill();
            ResetToOrigin();
        }

        private void ResetToOrigin()
        {
            if (_target == null) return;

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