using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private float spawnOffsetX = 1500f; // 목표 위치 기준 오른쪽에서 시작할 오프셋 거리

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
            // 1. 필요한 모든 요소 선제 활성화 (레이아웃 계산을 위해 필수)
            if (img_ChiefIcon != null) img_ChiefIcon.gameObject.SetActive(true);
            if (rect_Paching != null) rect_Paching.gameObject.SetActive(true);
            if (rect_Line != null) rect_Line.gameObject.SetActive(true);
            if (img_Background != null) img_Background.gameObject.SetActive(true);
            
            if (img_Pachings != null)
            {
                foreach (var pachingImg in img_Pachings)
                {
                    if (pachingImg != null) pachingImg.gameObject.SetActive(true);
                }
            }

            // 2. 레이아웃 강제 갱신 및 1프레임 대기
            // Inactive 상태에서 Active가 된 직후에는 한 프레임이 지나야 RectTransform의 정확한 World Position이 계산됩니다.
            Canvas.ForceUpdateCanvases();
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);

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

            // 3. 이제 정확해진 목표 위치 획득
            Vector3 finalTargetPos = rect_Target.position;
            Vector3 startPos = finalTargetPos + new Vector3(spawnOffsetX, 0, 0);

            // 4. 초기 상태 명시적 세팅
            img_ChiefIcon.sprite = chiefSprite;
            img_ChiefIcon.transform.position = startPos;

            rect_Line.sizeDelta = new Vector2(rect_Line.sizeDelta.x, 0);

            Color bgColor = img_Background.color;
            bgColor.a = 0f;
            img_Background.color = bgColor;

            rect_Paching.localRotation = Quaternion.identity;
            rect_Paching.position = rect_PachingTarget != null ? rect_PachingTarget.position : finalTargetPos;
            
            foreach (var pachingImg in img_Pachings)
            {
                if (pachingImg == null) continue;
                Color pColor = pachingImg.color;
                pColor.a = 0f;
                pachingImg.color = pColor;
            }

            // 5. 애니메이션 시퀀스 생성 및 실행
            var masterSeq = DOTween.Sequence();

            // 애니메이션 재생 중 타겟 위치가 변할 경우를 대비한 추적 (OnUpdate)
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
            
            foreach (var pachingImg in img_Pachings)
            {
                if (pachingImg == null) continue;
                masterSeq.Insert(closeStartTime, pachingImg.DOFade(0f, pachingFadeOutDuration));
            }

            // 시퀀스 완료 대기
            await masterSeq.ToUniTask(cancellationToken: ct);
            
            // 6. Cleanup (자식 오브젝트들 비활성화)
            img_ChiefIcon.gameObject.SetActive(false);
            img_Background.gameObject.SetActive(false);
            rect_Paching.gameObject.SetActive(false);
            rect_Line.gameObject.SetActive(false);
        }
    }
}
