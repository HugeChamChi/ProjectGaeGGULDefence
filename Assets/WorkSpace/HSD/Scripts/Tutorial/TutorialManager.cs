using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
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
                    _instance = FindFirstObjectByType<TutorialManager>();
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

        [Header("Default Target Settings")]
        [SerializeField] private Vector2 _defaultSizeOffset = new Vector2(10, 10);
        [SerializeField] private float _defaultSoftness = 10f;

        [Header("Focus Frame (Corners)")]
        [SerializeField] private RectTransform _focusFrame;
        [SerializeField] private Vector2 _focusFramePadding = new Vector2(20, 20);

        [Header("Guide Arrow")]
        [SerializeField] private RectTransform _guideArrow;
        [SerializeField] private Vector3 _guideArrowOffset = Vector3.zero;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // 씬 전환 이벤트 구독
            SceneManager.sceneLoaded += OnSceneLoaded;

            RefreshRegistry();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[TutorialManager] Scene Loaded: {scene.name}. Refreshing UI and Registry.");

            // 1. 새로운 씬의 타겟들 자동 등록
            RefreshRegistry();

            // 2. 만약 현재 UI 레퍼런스들이 사라졌다면(씬 로컬 UI 사용 시), 새로 검색하여 할당
            TryFindSceneLocalUI();
        }

        /// <summary>
        /// 씬에 배치된 튜토리얼용 UI 요소들을 자동으로 찾아 연결합니다.
        /// </summary>
        private void TryFindSceneLocalUI()
        {
            if (_highlighter == null) _highlighter = FindFirstObjectByType<UI_TutorialHighlighter>(FindObjectsInactive.Include);
            if (_raycastFilter == null) _raycastFilter = FindFirstObjectByType<UI_TutorialRaycastFilter>(FindObjectsInactive.Include);
            if (_dimCanvasGroup == null)
            {
                // 특정 태그나 이름을 가진 객체를 찾도록 규칙을 정할 수 있습니다.
                var dimObj = GameObject.Find("Tutorial_DimBackground");
                if (dimObj != null) _dimCanvasGroup = dimObj.GetComponent<CanvasGroup>();
            }
        }

        [Button]
        public void RefreshRegistry()
        {
            TutorialRegistry.Clear();
            var uiTargets = FindObjectsByType<UI_TutorialTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var target in uiTargets) TutorialRegistry.RegisterUI(target.UITargetID, target);
            var actors = FindObjectsByType<TutorialActor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var actor in actors) TutorialRegistry.RegisterActor(actor.ActorID, actor);
            Debug.Log($"[TutorialManager] Registry Refreshed: {uiTargets.Length} UI Targets, {actors.Length} Actors found in current scene.");
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
