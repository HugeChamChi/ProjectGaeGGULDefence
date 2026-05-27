using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        private static TutorialManager _instance;
        public static TutorialManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<TutorialManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("TutorialManager");
                        _instance = go.AddComponent<TutorialManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Background Control")]
        [SerializeField] private CanvasGroup _dimCanvasGroup; 
        [SerializeField] private float _dimFadeDuration = 0.2f;

        [Header("Highlighting (Raycast Hole)")]
        [SerializeField] private UI_TutorialHighlighter _highlighter;
        [SerializeField] private UI_TutorialRaycastFilter _raycastFilter;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshRegistry();
        }

        /// <summary>
        /// 씬 내의 모든 TutorialTarget과 Actor를 검색하여 등록합니다.
        /// </summary>
        [Button]
        public void RefreshRegistry()
        {
            TutorialRegistry.Clear();
            var uiTargets = FindObjectsOfType<UI_TutorialTarget>(true);
            foreach (var target in uiTargets) TutorialRegistry.RegisterUI(target.UITargetID, target);
            var actors = FindObjectsOfType<TutorialActor>(true);
            foreach (var actor in actors) TutorialRegistry.RegisterActor(actor.ActorID, actor);
            Debug.Log($"[TutorialManager] Registry Refreshed: {uiTargets.Length} UI Targets, {actors.Length} Actors.");
        }

        public void SetBlockInteraction(bool active)
        {
            if (_dimCanvasGroup == null) return;
            _dimCanvasGroup.blocksRaycasts = active;
        }

        /// <summary>
        /// 물리적인 클릭 구멍(Raycast Hole)만 엽니다.
        /// </summary>
        public void SetInteractionTarget(string id)
        {
            // 동적 생성 대응을 위해 갱신 후 검색
            RefreshRegistry();
            var target = TutorialRegistry.GetUI(id);
            if (target != null && _raycastFilter != null)
            {
                SetBlockInteraction(true); 
                _raycastFilter.SetTarget(target.GetComponent<RectTransform>());
            }
        }

        public async UniTask SetDimAsync(bool active)
        {
            if (_dimCanvasGroup == null) return;
            float targetAlpha = active ? 1f : 0f;
            if (Mathf.Approximately(_dimCanvasGroup.alpha, targetAlpha)) return;
            await _dimCanvasGroup.DOFade(targetAlpha, _dimFadeDuration).SetUpdate(true).ToUniTask();
        }

        public void ShowHighlight(string id)
        {
            // 하이라이트 전에도 한 번 갱신하여 방금 생성된 UI를 찾을 수 있게 함
            RefreshRegistry();

            var target = TutorialRegistry.GetUI(id);
            if (target == null) return;
            if (_highlighter != null) _highlighter.SetTarget(target.GetComponent<RectTransform>(), target.SizeOffset, target.Softness);
            SetInteractionTarget(id);
        }

        public void HideHighlight()
        {
            if (_highlighter != null) _highlighter.Hide();
            if (_raycastFilter != null) _raycastFilter.Clear();
        }

        public async UniTask PlaySequenceAsync(TutorialSequence sequence)
        {
            if (sequence == null) return;
            
            // 이미 완료된 튜토리얼인지 체크
            if (Player.Tutorial.IsCompleted(sequence.tutorialID))
            {
                Debug.Log($"[TutorialManager] Sequence '{sequence.tutorialID}' is already completed. Skipping.");
                return;
            }

            Debug.Log($"[TutorialManager] Sequence Start: {sequence.tutorialID}");
            
            SetBlockInteraction(true);
            await sequence.PlayAsync(this);
            
            // 완료 상태 저장
            Player.Tutorial.MarkAsCompleted(sequence.tutorialID);

            await SetDimAsync(false);
            SetBlockInteraction(false);
            HideHighlight();

            Debug.Log($"[TutorialManager] Sequence End: {sequence.tutorialID}");
        }

        public async UniTask WaitAnyClick()
        {
            if (_raycastFilter != null) _raycastFilter.Clear();
            if (_dimCanvasGroup != null && _dimCanvasGroup.TryGetComponent<Button>(out var btn)) await btn.OnClickAsync();
            else await UniTask.Delay(1000);
        }

        public async UniTask WaitTargetClick(string targetID)
        {
            // 1. 안전 대기 (UI 생성 및 레이아웃 갱신 시간)
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));

            // 2. 동적 생성된 UI를 위해 레지스트리 최신화
            RefreshRegistry();

            var target = TutorialRegistry.GetUI(targetID);
            if (target == null)
            {
                Debug.LogError($"[Tutorial] WaitTargetClick 실패: ID '{targetID}'를 찾을 수 없습니다.");
                return;
            }

            if (!target.TryGetComponent<Button>(out var button))
            {
                Debug.LogError($"[Tutorial] WaitTargetClick 실패: '{targetID}'에 Button 컴포넌트가 없습니다.");
                return;
            }

            // 3. 물리적 클릭 구멍 열기
            SetInteractionTarget(targetID);
            
            try 
            {
                if (!button.gameObject.activeInHierarchy || !button.interactable)
                {
                    Debug.LogWarning($"[Tutorial] '{targetID}'이 비활성 상태입니다. 대기를 스킵합니다.");
                    return;
                }

                await button.OnClickAsync(button.GetCancellationTokenOnDestroy());
                Debug.Log($"[TutorialManager] Target clicked: {targetID}");
            }
            catch (OperationCanceledException) 
            {
                Debug.Log($"[TutorialManager] Target {targetID} was destroyed after click.");
            }
            finally 
            {
                if (_raycastFilter != null) _raycastFilter.Clear();
            }
        }

        [Button] public void TestPlay(TutorialSequence sequence) => PlaySequenceAsync(sequence).Forget();
    }
}
