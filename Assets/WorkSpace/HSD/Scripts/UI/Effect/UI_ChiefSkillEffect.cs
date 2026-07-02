using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HSD.UI.Effect
{
    public class UI_ChiefSkillEffect : UI_Base
    {

        [Header("UI References - Elements")]
        [SerializeField] private Image img_ChiefIcon;
        [SerializeField] private RectTransform rect_Line;
        [SerializeField] private Image img_Background;
        [SerializeField] private RectTransform rect_Target; // 고정된 도착 목표 위치 (인스펙터 할당)

        [Header("UI References - Paching")]
        [SerializeField] private RectTransform rect_Paching;
        [SerializeField] private RectTransform rect_PachingTarget; // 파칭이 표시될 고정 목표 위치 (인스펙터 할당)
        [SerializeField] private Image[] img_Pachings; // Paching을 구성하는 2개 이상의 이미지들 (Alpha 조절용)

        [Header("Line Animation Settings")]
        [SerializeField] private float lineOpenHeight = 500f;
        [SerializeField] private float lineOpenDuration = 0.4f;
        [SerializeField] private Ease lineOpenEase = Ease.OutCubic;
        [SerializeField] private float lineCloseDuration = 0.3f;
        [SerializeField] private Ease lineCloseEase = Ease.InCubic;

        [Header("Chief Icon Settings")]
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private Ease moveEase = Ease.OutBack;
        [SerializeField] private RectTransform spawnPoint;

        [Header("Background Settings")]
        [SerializeField] private float bgDarkAlpha = 0.6f;

        [Header("Paching Settings")]
        [SerializeField] [Range(0f, 1f)] private float pachingStartRatio = 0.3f; // 이동 중 파칭이 시작될 타이밍 비율 (0=동시, 1=도착 후)
        [SerializeField] private Vector3 pachingRotateAngle = new Vector3(0, 0, -360f);
        [SerializeField] private float pachingRotateDuration = 0.5f;
        [SerializeField] private Ease pachingRotateEase = Ease.OutCubic;
        [SerializeField] private float pachingMaxAlpha = 1f; // Paching 도달 시 최대 Alpha 값
        [SerializeField] private float pachingFadeInDuration = 0.15f;
        [SerializeField] private float pachingFadeOutDuration = 0.2f;

        protected override async UniTask OpenAnimationAsync()
        {
            // 이펙트 뷰는 PlayEffectAsync에서 자체 애니메이션을 수행하므로 기본 Open 애니메이션은 무시함
            await UniTask.CompletedTask;
        }

        protected override async UniTask CloseAnimationAsync()
        {
            // Close도 마찬가지로 별도의 애니메이션 없이 즉시 종료
            await UniTask.CompletedTask;
        }

        public async UniTask PlayEffectAsync(Sprite chiefSprite, CancellationToken ct)
        {
            // 0. 루트 오브젝트 및 부모 캔버스 활성화 확인
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                // SetActive(true) 호출 시 Awake()가 동기적으로 실행됨
            }

            // 1. 필요한 모든 요소 활성화 (레이아웃 계산을 위해 필수)
            // 초기 상태는 투명하게 하여 레이아웃 계산 중 잔상이 보이지 않게 함
            PrepareInitialVisibility();

            // 2. 레이아웃이 잡힐 때까지 대기
            // Inactive 상태였던 오브젝트는 활성화 직후 1~2프레임 정도 위치값이 (0,0,0)일 수 있음
            Canvas.ForceUpdateCanvases();
            
            // 위치값이 제대로 잡힐 때까지 최대 3프레임 대기 (보통 1프레임이면 충분)
            int timeout = 0;
            while (timeout < 3)
            {
                if (rect_Target != null && rect_Target.position != Vector3.zero) break;
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken: ct);
                timeout++;
            }

            if (img_ChiefIcon == null || rect_Line == null || img_Background == null || rect_Paching == null || img_Pachings == null || img_Pachings.Length == 0)
            {
                Debug.LogError("UI_ChiefSkillEffect: Missing UI references.");
                return;
            }

            if (rect_Target == null)
            {
                Debug.LogError("UI_ChiefSkillEffect: rect_Target is not assigned in the Inspector.");
                return;
            }

            // 3. 정확해진 목표 위치 획득 및 초기 위치 설정
            Vector3 finalTargetPos = rect_Target.position;
            

            // 데이터 적용 및 초기 위치 강제 이동
            img_ChiefIcon.sprite = chiefSprite;
            img_ChiefIcon.transform.position = spawnPoint.position;
            rect_Line.sizeDelta = new Vector2(rect_Line.sizeDelta.x, 0);
            
            rect_Paching.localRotation = Quaternion.identity;
            rect_Paching.position = rect_PachingTarget != null ? rect_PachingTarget.position : finalTargetPos;

            // 4. 애니메이션 시퀀스 생성 및 실행
            var masterSeq = DOTween.Sequence();

            masterSeq.OnUpdate(() =>
            {
                if (rect_Paching != null && rect_Paching.gameObject.activeSelf)
                {
                    rect_Paching.position = rect_PachingTarget != null ? rect_PachingTarget.position : finalTargetPos;
                }
            });

            // 연출 단계 1: 라인 확장, 아이콘 이동, 배경 어두워짐
            masterSeq.Insert(0f, rect_Line.DOSizeDelta(new Vector2(rect_Line.sizeDelta.x, lineOpenHeight), lineOpenDuration).SetEase(lineOpenEase));
            masterSeq.Insert(0f, img_ChiefIcon.transform.DOMove(finalTargetPos, moveDuration).SetEase(moveEase));
            masterSeq.Insert(0f, img_Background.DOFade(bgDarkAlpha, lineOpenDuration));

            // 연출 단계 2: 파칭 연출 시작
            float pachingStartTime = moveDuration * pachingStartRatio; 
            masterSeq.Insert(pachingStartTime, rect_Paching.DORotate(pachingRotateAngle, pachingRotateDuration, RotateMode.FastBeyond360).SetEase(pachingRotateEase));
            
            foreach (var pachingImg in img_Pachings)
            {
                if (pachingImg == null) continue;
                masterSeq.Insert(pachingStartTime, pachingImg.DOFade(pachingMaxAlpha, pachingFadeInDuration));
            }

            // 연출 단계 3: 마무리 (라인 닫힘, 배경 및 파칭 페이드 아웃)
            float closeStartTime = pachingStartTime + (pachingRotateDuration * 0.8f); 
            
            masterSeq.Insert(closeStartTime, rect_Line.DOSizeDelta(new Vector2(rect_Line.sizeDelta.x, 0), lineCloseDuration).SetEase(lineCloseEase));
            masterSeq.Insert(closeStartTime, img_Background.DOFade(0f, lineCloseDuration));
            masterSeq.Insert(closeStartTime, img_ChiefIcon.DOFade(0f, lineCloseDuration));
            
            foreach (var pachingImg in img_Pachings)
            {
                if (pachingImg == null) continue;
                masterSeq.Insert(closeStartTime, pachingImg.DOFade(0f, pachingFadeOutDuration));
            }

            // 시퀀스 완료 대기
            await masterSeq.ToUniTask(cancellationToken: ct);
            
            // 5. Cleanup (자식 오브젝트들 비활성화)
            img_ChiefIcon.gameObject.SetActive(false);
            img_Background.gameObject.SetActive(false);
            rect_Paching.gameObject.SetActive(false);
            rect_Line.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 레이아웃 계산을 위해 요소를 활성화하되, 잔상이 보이지 않도록 투명하게 설정합니다.
        /// </summary>
        private void PrepareInitialVisibility()
        {
            if (img_ChiefIcon != null)
            {
                img_ChiefIcon.gameObject.SetActive(true);
                Color c = img_ChiefIcon.color;
                c.a = 1f;
                img_ChiefIcon.color = c;
            }
            if (rect_Paching != null) rect_Paching.gameObject.SetActive(true);
            if (rect_Line != null) rect_Line.gameObject.SetActive(true);
            
            if (img_Background != null)
            {
                img_Background.gameObject.SetActive(true);
                Color c = img_Background.color;
                c.a = 0f;
                img_Background.color = c;
            }

            if (img_Pachings != null)
            {
                foreach (var pachingImg in img_Pachings)
                {
                    if (pachingImg == null) continue;
                    pachingImg.gameObject.SetActive(true);
                    Color c = pachingImg.color;
                    c.a = 0f;
                    pachingImg.color = c;
                }
            }
        }
    }
}
