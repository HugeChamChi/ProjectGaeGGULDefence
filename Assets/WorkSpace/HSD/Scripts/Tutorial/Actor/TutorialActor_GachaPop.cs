using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>
    /// 아이템이 팝업되고 목적지로 이동하는 가챠 전용 연출 Mono입니다.
    /// </summary>
    public class TutorialActor_GachaPop : TutorialActor
    {
        [Header("References")]
        [SerializeField] private RectTransform _itemUI;      // 뿅 나타날 아이템 UI
        [SerializeField] private RectTransform _targetPos;   // 최종 목적지 위치 (UI)
        [SerializeField] private Vector2 _targetPosOffset;

        [Header("Settings")]
        [SerializeField] private bool _useBackground = true; // 배경 자동 제어 여부
        [SerializeField] private float _popDuration = 0.3f;   // 처음 커지는 시간
        [SerializeField] private float _moveDuration = 0.4f;  // 목적지 이동 시간
        [SerializeField] private float _arrivalDelay = 0.1f;    // 도착 후 반응까지의 짧은 대기 시간
        [SerializeField] private float _arrivalScale = 1.3f;  // 도착 시 커지는 정도 (Punch Scale 기준)
        [SerializeField] private float _scaleDuration = 0.25f;

        public override async UniTask PlayAsync()
        {
            if (_itemUI == null || _targetPos == null)
            {
                Debug.LogError($"[TutorialActor_GachaPop] References are missing on {gameObject.name}");
                return;
            }

            // 1. 초기화 (중앙에서 크기 0으로 시작)
            _itemUI.localScale = Vector3.zero;
            _itemUI.gameObject.SetActive(true);

            // 배경 켜기
            if (_useBackground) await TutorialManager.Instance.SetDimAsync(true);

            // 2. [Step 1] 뿅! 하고 나타남
            await _itemUI.DOScale(1f, _popDuration)
                .SetEase(Ease.OutBack)
                .ToUniTask();

            // 잠깐 대기 (쫀득한 타이밍)
            await UniTask.Delay(200);

            // 3. [Step 2] 목적지로 빠르게 이동
            _itemUI.DOMove(_targetPos.position + (Vector3)_targetPosOffset, _moveDuration).SetEase(Ease.InQuad);
            
            // 이동 시간만큼 대기
            await UniTask.Delay(TimeSpan.FromSeconds(_arrivalDelay));

            // 4. [Step 3] 도착 시 반응 (커졌다가 팅기는 Punch 효과)
            await _itemUI.DOPunchScale(Vector3.one * (_arrivalScale - 1f), _scaleDuration, 5, 1f)
                .SetEase(Ease.OutQuad)
                .ToUniTask();

            // 배경 끄기
            if (_useBackground) await TutorialManager.Instance.SetDimAsync(false);

            _itemUI.gameObject.SetActive(false);


            Debug.Log($"[Tutorial] {ActorID} 연출 완료");
        }
    }
}
