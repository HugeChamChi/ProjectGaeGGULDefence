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
        [SerializeField] private bool useCustomBaseScale = false;
        [SerializeField] private Vector3 customBaseScale = Vector3.one;

        private Vector2 _currentBreath = Vector2.one;

        public override async UniTask Play()
        {
            Stop();
            
            Vector3 baseScale = useCustomBaseScale ? customBaseScale : _originScale;
            _currentBreath = Vector2.one;

            _currentSeq = DOTween.Sequence()
                .SetUpdate(ignoreTimeScale)
                .Append(DOTween.To(() => _currentBreath, x => _currentBreath = x, scaleMultiplier, duration).SetEase(ease))
                .Append(DOTween.To(() => _currentBreath, x => _currentBreath = x, Vector2.one, duration).SetEase(ease))
                .OnUpdate(() =>
                {
                    if (_target != null)
                    {
                        // 외부(UnitBase 등)에서 유닛을 좌우 반전시켰을 때 애니메이션이 이를 덮어쓰거나 
                        // 반대로 트위닝되어 뒤집어지는(Flip) 현상을 막기 위해 항상 현재 로컬 스케일의 부호를 유지합니다.
                        float signX = _target.localScale.x < 0 ? -1f : 1f;
                        float signY = _target.localScale.y < 0 ? -1f : 1f;
                        float signZ = _target.localScale.z < 0 ? -1f : 1f;

                        _target.localScale = new Vector3(
                            signX * Mathf.Abs(baseScale.x) * _currentBreath.x,
                            signY * Mathf.Abs(baseScale.y) * _currentBreath.y,
                            signZ * Mathf.Abs(baseScale.z)
                        );
                    }
                })
                .SetDelay(delay);

            if (isLoop)
            {
                _ = _currentSeq.SetLoops(-1);
            }

            await _currentSeq.Play().ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }
}
