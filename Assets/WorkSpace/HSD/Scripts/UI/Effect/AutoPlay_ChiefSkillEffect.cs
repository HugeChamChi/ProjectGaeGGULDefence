using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HSD.UI.Effect.Tests
{
    public class AutoPlay_ChiefSkillEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UI_ChiefSkillEffect effectView;

        [Header("Auto Play Settings")]
        [SerializeField] private bool autoPlayOnStart = true;
        [SerializeField] private float playIntervalSeconds = 4f;

        [Header("Test Data")]
        [SerializeField] private Sprite testChiefSprite;

        private ChiefSkillEffectPresenter _presenter;
        private bool _isPlaying;

        private void Start()
        {
            if (effectView != null)
            {
                effectView.gameObject.SetActive(false);
                _presenter = new ChiefSkillEffectPresenter(effectView);
            }
            else
            {
                Debug.LogError("AutoPlay_ChiefSkillEffect: UI_ChiefSkillEffect reference is missing.");
                return;
            }

            if (autoPlayOnStart)
            {
                StartAutoPlay();
            }
        }

        [Button]
        public void StartAutoPlay()
        {
            if (_isPlaying) return;
            _isPlaying = true;
            Debug.Log($"AutoPlay_ChiefSkillEffect: Started. Interval: {playIntervalSeconds}s");
            AutoPlayLoopAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        [Button]
        public void StopAutoPlay()
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            Debug.Log("AutoPlay_ChiefSkillEffect: Stopped.");
        }

        private async UniTaskVoid AutoPlayLoopAsync(CancellationToken ct)
        {
            while (_isPlaying && !ct.IsCancellationRequested)
            {
                if (_presenter != null)
                {
                    // 이펙트 실행 (Fire and Forget)
                    _presenter.ExecuteSkillEffectAsync(testChiefSprite, ct).Forget();
                }

                // 지정된 시간만큼 대기
                await UniTask.Delay(TimeSpan.FromSeconds(playIntervalSeconds), cancellationToken: ct);
            }
        }
    }
}
