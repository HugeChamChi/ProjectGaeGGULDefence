using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    public enum AnimTriggerType
    {
        None,
        OnEnable
    }

    /// <summary>
    /// 모든 애니메이션 스크립트의 기반이 되는 베이스 클래스입니다.
    /// </summary>
    public abstract class Anim_Base : MonoBehaviour, ITweenEffect
    {
        [Header("Common Settings")]
        [SerializeField] protected AnimTriggerType triggerType = AnimTriggerType.OnEnable;
        [SerializeField] protected bool isLoop;
        [SerializeField] protected Ease ease = Ease.OutQuad;
        [SerializeField] protected float duration = 0.5f;
        [SerializeField] protected float delay = 0f;

        protected Transform _target;
        protected RectTransform _rect;
        protected bool _isUI;
        protected Vector3 _originPos;
        protected Vector3 _originSize;
        protected Sequence _currentSeq;

        protected virtual void Awake()
        {
            Initialize(transform);
        }

        protected virtual void OnEnable()
        {
            if (triggerType == AnimTriggerType.OnEnable)
            {
                Play().Forget();
            }
        }

        protected virtual void OnDisable()
        {
            Stop();
        }

        public virtual void Initialize(Transform target)
        {
            _target = target;
            _rect = target as RectTransform;
            _isUI = _rect != null;

            if (_isUI)
            {
                _originPos = _rect.anchoredPosition;
                _originSize = _rect.sizeDelta;
            }
            else
            {
                _originPos = target.localPosition;
                _originSize = target.localScale;
            }
        }

        public abstract UniTask Play();

        public virtual void Stop()
        {
            if (_currentSeq != null)
            {
                _currentSeq.Kill();
                _currentSeq = null;
            }

            if (_target != null)
            {
                _target.DOKill();
            }

            ResetToOrigin();
        }

        protected virtual void ResetToOrigin()
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

        protected Tweener GetMoveTween(Vector3 pos, float dur) => _isUI ? _rect.DOAnchorPos(pos, dur) : _target.DOLocalMove(pos, dur);
        protected Tweener GetSizeTween(Vector3 size, float dur) => _isUI ? _rect.DOSizeDelta(size, dur) : _target.DOScale(size, dur);
    }
}
