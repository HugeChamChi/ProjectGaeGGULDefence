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
        [Header("Target Settings")]
        [SerializeField] protected Transform animationTarget;

        [Header("Common Settings")]
        [SerializeField] protected AnimTriggerType triggerType = AnimTriggerType.OnEnable;
        [SerializeField] protected bool ignoreTimeScale = false;
        [SerializeField] protected bool isLoop;
        [SerializeField] protected Ease ease = Ease.OutQuad;
        [SerializeField] protected float duration = 0.5f;
        [SerializeField] protected float delay = 0f;

        protected Transform _target;
        protected RectTransform _rect;
        protected bool _isUI;
        protected Vector3 _originPos;
        protected Vector3 _originScale;
        protected Sequence _currentSeq;

        protected virtual void Awake()
        {
            Initialize(animationTarget != null ? animationTarget : transform);
        }

        protected virtual void Reset()
        {
            if (animationTarget == null) animationTarget = transform;
            
            // UI 요소(RectTransform)가 있으면 기본적으로 ignoreTimeScale을 true로 설정
            ignoreTimeScale = GetComponent<RectTransform>() != null;
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

            _originPos = _isUI ? _rect.anchoredPosition : target.localPosition;
            _originScale = target.localScale;
        }

        public abstract UniTask Play();

        public virtual void Stop()
        {
            KillCurrentTween();
            ResetToOrigin();
        }

        /// <summary>
        /// 현재 실행 중인 트윈을 즉시 중단합니다. (상태 리셋 없음)
        /// </summary>
        protected void KillCurrentTween()
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
        }

        protected virtual void ResetToOrigin()
        {
            if (_target == null) return;

            if (_isUI)
            {
                _rect.anchoredPosition = _originPos;
            }
            else
            {
                _target.localPosition = _originPos;
            }

            _target.localScale = _originScale;
        }

        protected Tweener GetMoveTween(Vector3 pos, float dur) => _isUI ? _rect.DOAnchorPos(pos, dur) : _target.DOLocalMove(pos, dur);
        protected Tweener GetScaleTween(Vector3 scale, float dur) => _target.DOScale(scale, dur);
    }
}
