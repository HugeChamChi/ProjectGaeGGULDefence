using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<SceneComponentCollection> _scenes = new();

        /// <summary>씬 DI 초기화에서 대상 제공자를 등록한다.</summary>
        public void RegisterScene(SceneComponentCollection components)
        {
            if (!_scenes.Contains(components)) _scenes.Add(components);
            RefreshRegistry();
        }

        /// <summary>씬 종료 때 해당 씬의 대상 참조를 해제한다.</summary>
        public void UnregisterScene(SceneComponentCollection components)
        {
            _scenes.Remove(components);
            RefreshRegistry();
        }

        [Header("Background Control")]
        [SerializeField] private CanvasGroup _dimCanvasGroup;
        [SerializeField] private float _dimFadeDuration = 0.2f;

        [Header("Highlighting (Raycast Hole)")]
        [SerializeField] private UI_TutorialHighlighter _highlighter;
        [SerializeField] private UI_TutorialRaycastFilter _raycastFilter;

        [Header("Default Target Settings")]
        [SerializeField] private Vector2 _defaultSizeOffset = new Vector2(10, 10);
        [SerializeField] private float _defaultSoftness = 10f;

        [Header("Focus Frame (Corners)")]
        [SerializeField] private RectTransform _focusFrame;
        [SerializeField] private Vector2 _focusFramePadding = new Vector2(20, 20);

        [Header("Guide Arrow")]
        [SerializeField] private RectTransform _guideArrow;
        [SerializeField] private Vector3 _guideArrowOffset = Vector3.zero;

        /// <summary>앱 서비스가 종료되면 씬 대상 캐시를 정리한다.</summary>
        private void OnDestroy()
        {
            _scenes.Clear();
            TutorialRegistry.Clear();
        }

        /// <summary>등록된 씬의 비활성 대상까지 갱신한다. 전역 객체 검색은 사용하지 않는다.</summary>
        [Button]
        public void RefreshRegistry()
        {
            TutorialRegistry.Clear();
            foreach (var scene in _scenes)
            {
                foreach (var target in scene.Enumerate<UI_TutorialTarget>()) TutorialRegistry.RegisterUI(target.UITargetID, target);
                foreach (var actor in scene.Enumerate<TutorialActor>()) TutorialRegistry.RegisterActor(actor.ActorID, actor);
            }
        }

        public void SetBlockInteraction(bool active)
        {
            if (_dimCanvasGroup == null) return;
            _dimCanvasGroup.blocksRaycasts = active;
        }

        public void SetInteractionTarget(string id)
        {
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
            var target = TutorialRegistry.GetUI(id);
            if (target == null) return;
            
            RectTransform targetRect = target.GetComponent<RectTransform>();
            
            if (_highlighter != null) _highlighter.SetTarget(targetRect, _defaultSizeOffset, _defaultSoftness);
            SetInteractionTarget(id);

            if (_focusFrame != null)
            {
                _focusFrame.gameObject.SetActive(true);
                
                Vector3[] corners = new Vector3[4];
                targetRect.GetWorldCorners(corners);
                
                Vector3 center = (corners[0] + corners[2]) * 0.5f;
                _focusFrame.position = center;
                _focusFrame.sizeDelta = targetRect.rect.size + _focusFramePadding;
            }

            if (_guideArrow != null)
            {
                _guideArrow.gameObject.SetActive(true);
                
                Vector3[] corners = new Vector3[4];
                targetRect.GetWorldCorners(corners);
                
                // corners[1] is Top-Left, corners[2] is Top-Right
                Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
                _guideArrow.position = topCenter + _guideArrowOffset;
            }
        }

        public void HideHighlight()
        {
            if (_highlighter != null) _highlighter.Hide();
            if (_raycastFilter != null) _raycastFilter.Clear();
            if (_focusFrame != null) _focusFrame.gameObject.SetActive(false);
            if (_guideArrow != null) _guideArrow.gameObject.SetActive(false);
        }

        public async UniTask PlaySequenceAsync(TutorialSequence sequence)
        {
            if (sequence == null) return;

            if (Player.Tutorial.IsCompleted(sequence.tutorialID))
            {
                Debug.Log($"[TutorialManager] Sequence '{sequence.tutorialID}' is already completed. Skipping.");
                return;
            }

            SetBlockInteraction(true);
            await sequence.PlayAsync(this);

            Player.Tutorial.MarkAsCompleted(sequence.tutorialID);
            await SetDimAsync(false);
            SetBlockInteraction(false);
            HideHighlight();
        }

        public async UniTask WaitAnyClick()
        {
            if (_raycastFilter != null) _raycastFilter.Clear();
            if (_dimCanvasGroup != null && _dimCanvasGroup.TryGetComponent<Button>(out var btn)) await btn.OnClickAsync();
            else await UniTask.Delay(1000);
        }

        public async UniTask WaitTargetClick(string targetID)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.1f));

            var target = TutorialRegistry.GetUI(targetID);
            if (target == null) return;

            if (!target.TryGetComponent<Button>(out var button)) return;

            SetInteractionTarget(targetID);
            try
            {
                if (!button.gameObject.activeInHierarchy || !button.interactable) return;
                await button.OnClickAsync(button.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (_raycastFilter != null) _raycastFilter.Clear();
            }
        }

        [Button] public void TestPlay(TutorialSequence sequence) => PlaySequenceAsync(sequence).Forget();
    }
}
