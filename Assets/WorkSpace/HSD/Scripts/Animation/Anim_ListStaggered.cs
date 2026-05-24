using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Animation
{
    /// <summary>
    /// 리스트 형태의 UI 요소들을 순차적으로(Staggered) 등장/퇴장시키는 애니메이션 제어 클래스입니다.
    /// 슬롯 프리팹에 별도의 애니메이션 컴포넌트가 없어도 Transform을 직접 제어하여 연출합니다.
    /// </summary>
    public class Anim_ListStaggered : MonoBehaviour
    {
        [Header("Stagger Settings")]
        [SerializeField] private float interval = 0.05f;
        [SerializeField] private bool useRealtime = true;

        [Header("Slot Animation Settings")]
        [SerializeField] private float slotDuration = 0.3f;
        [SerializeField] private Ease slotEase = Ease.OutBack;
        [SerializeField] private Vector2 slotStartMultiplier = Vector2.zero;

        [Header("Particle Settings")]
        [SerializeField] private GameObject appearParticlePrefab;
        [SerializeField] private Vector3 particleOffset;
        [SerializeField] private bool usePooling = true;
        [SerializeField] private float particleDestroyDelay = 2f;

        private CancellationTokenSource _cts;
        private readonly List<Transform> _activeTargets = new List<Transform>();

        private void OnDestroy()
        {
            CancelCurrentTasks();
        }

        /// <summary>
        /// 제공된 슬롯들의 In 애니메이션을 순차적으로 재생합니다.
        /// </summary>
        public async UniTask PlayInAsync<T>(IEnumerable<T> slots) where T : Component
        {
            CancelCurrentTasks();
            _cts = new CancellationTokenSource();

            var tasks = new List<UniTask>();
            int index = 0;

            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    Transform target = slot.transform;
                    _activeTargets.Add(target);
                    
                    // 시작 시점에 즉시 초기화하여 '깜빡임' 현상 방지
                    target.DOKill();
                    target.localScale = new Vector3(slotStartMultiplier.x, slotStartMultiplier.y, 1f);
                    
                    tasks.Add(PlayWithDelay(target, true, index * interval, _cts.Token));
                    index++;
                }
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// 제공된 슬롯들의 Out 애니메이션을 순차적으로 재생합니다.
        /// </summary>
        public async UniTask PlayOutAsync<T>(IEnumerable<T> slots) where T : Component
        {
            CancelCurrentTasks();
            _cts = new CancellationTokenSource();

            var tasks = new List<UniTask>();
            int index = 0;

            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    Transform target = slot.transform;
                    _activeTargets.Add(target);
                    tasks.Add(PlayWithDelay(target, false, index * interval, _cts.Token));
                    index++;
                }
            }

            if (tasks.Count > 0)
                await UniTask.WhenAll(tasks);
        }

        public void Skip()
        {
            _cts?.Cancel();
            foreach (var target in _activeTargets)
            {
                if (target != null)
                {
                    target.DOKill();
                    target.localScale = Vector3.one; // Skip 시 최종 크기로 즉시 변경
                }
            }
            _activeTargets.Clear();
        }

        private void CancelCurrentTasks()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            foreach (var target in _activeTargets)
            {
                if (target != null) target.DOKill();
            }
            _activeTargets.Clear();
        }

        private async UniTask PlayWithDelay(Transform target, bool isIn, float delay, CancellationToken token)
        {
            if (delay > 0)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), delayType: useRealtime ? DelayType.Realtime : DelayType.DeltaTime, cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (token.IsCancellationRequested || target == null) return;

            target.DOKill();

            if (isIn)
            {
                // 파티클 생성
                if (appearParticlePrefab != null)
                {
                    SpawnParticle(target);
                }

                // 작았다가 커지는 연출
                target.localScale = new Vector3(slotStartMultiplier.x, slotStartMultiplier.y, 1f);
                await target.DOScale(Vector3.one, slotDuration).SetEase(slotEase).SetUpdate(useRealtime).ToUniTask(cancellationToken: token).SuppressCancellationThrow();
            }
            else
            {
                // 작아지며 사라지는 연출
                await target.DOScale(new Vector3(slotStartMultiplier.x, slotStartMultiplier.y, 1f), slotDuration).SetEase(Ease.InBack).SetUpdate(useRealtime).ToUniTask(cancellationToken: token).SuppressCancellationThrow();
            }
        }

        private void SpawnParticle(Transform parent)
        {
            // ParticleImage는 UI 계층 구조 안에 있어야 하므로 해당 슬롯을 부모로 설정합니다.
            var particle = RM.Instantiate(appearParticlePrefab, parent, usePooling);
            if (particle != null)
            {
                particle.transform.localPosition = particleOffset;
                RM.Destroy(particle, particleDestroyDelay);
            }
        }
    }
}
