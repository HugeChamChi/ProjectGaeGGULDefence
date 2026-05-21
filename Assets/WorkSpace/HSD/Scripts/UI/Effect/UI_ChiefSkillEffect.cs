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
        [SerializeField] private float lineCloseDuration = 0.3f;

        [Header("Chief Icon Settings")]
        [SerializeField] private float moveDuration = 0.4f;
        [SerializeField] private float spawnOffsetX = 1500f; // 목표 위치 기준 오른쪽에서 시작할 오프셋 거리

        [Header("Background Settings")]
        [SerializeField] private float bgDarkAlpha = 0.6f;

        [Header("Paching Settings")]
        [SerializeField] [Range(0f, 1f)] private float pachingStartRatio = 0.3f; // 이동 중 파칭이 시작될 타이밍 비율 (0=동시, 1=도착 후)
        [SerializeField] private Vector3 pachingRotateAngle = new Vector3(0, 0, -360f);
        [SerializeField] private float pachingRotateDuration = 0.5f;
        [SerializeField] private float pachingMaxAlpha = 1f; // Paching 도달 시 최대 Alpha 값
        [SerializeField] private float pachingFadeInDuration = 0.15f;
        [SerializeField] private float pachingFadeOutDuration = 0.2f;

        public async UniTask PlayEffectAsync(Sprite chiefSprite, CancellationToken ct)
        {
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

            // 고정 목표 위치 (인스펙터 할당 필수)
            Vector3 finalTargetPos = rect_Target.position;

            // Init State
            img_ChiefIcon.sprite = chiefSprite;
            img_ChiefIcon.gameObject.SetActive(true);
            
            // Paching Init (초기엔 투명하게)
            rect_Paching.gameObject.SetActive(true);
            rect_Paching.localRotation = Quaternion.identity;
            
            // 파칭 목표 위치가 별도로 지정되어 있다면 해당 위치 사용, 아니면 족장 아이콘의 도착 위치(finalTargetPos) 사용
            rect_Paching.position = rect_PachingTarget != null ? rect_PachingTarget.position : finalTargetPos;

            foreach (var pachingImg in img_Pachings)
            {
                if (pachingImg == null) continue;
                Color pColor = pachingImg.color;
                pColor.a = 0f;
                pachingImg.color = pColor;
                pachingImg.gameObject.SetActive(true);
            }
            
            // BG Init
            Color bgColor = img_Background.color;
            bgColor.a = 0f;
            img_Background.color = bgColor;
            img_Background.gameObject.SetActive(true);

            // Calculate Initial Position (목표 위치에서 오른쪽으로 지정된 오프셋만큼 떨어진 곳)
            Vector3 startPos = finalTargetPos + new Vector3(spawnOffsetX, 0, 0);
            img_ChiefIcon.transform.position = startPos;

            // Reset line height
            rect_Line.sizeDelta = new Vector2(rect_Line.sizeDelta.x, 0);

            // -----------------------------------------------------
            // Master Sequence 생성 (전체 애니메이션이 유기적으로 연결되도록 구성)
            // -----------------------------------------------------
            var masterSeq = DOTween.Sequence();

            // [추가됨] 애니메이션 재생 내내 Paching 위치를 지속적으로 추적하도록 설정
            masterSeq.OnUpdate(() =>
            {
                if (rect_Paching != null && rect_Paching.gameObject.activeSelf)
                {
                    rect_Paching.position = rect_PachingTarget != null ? rect_PachingTarget.position : finalTargetPos;
                }
            });

            // 1. [Time: 0.0s] Line Open & Icon Move & Background Darken
            masterSeq.Insert(0f, rect_Line.DOSizeDelta(new Vector2(rect_Line.sizeDelta.x, lineOpenHeight), lineOpenDuration).SetEase(Ease.OutCubic));
            masterSeq.Insert(0f, img_ChiefIcon.transform.DOMove(finalTargetPos, moveDuration).SetEase(Ease.OutBack)); // OutBack으로 튀어나오는 느낌 부여
            masterSeq.Insert(0f, img_Background.DOFade(bgDarkAlpha, lineOpenDuration));

            // 2. 파칭 시작 (나오는 도중에 파칭이 시작되도록 설정된 비율에 따라 타이밍 조절)
            float pachingStartTime = moveDuration * pachingStartRatio; 
            masterSeq.Insert(pachingStartTime, rect_Paching.DORotate(pachingRotateAngle, pachingRotateDuration, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            
            foreach (var pachingImg in img_Pachings)
            {
                if (pachingImg == null) continue;
                masterSeq.Insert(pachingStartTime, pachingImg.DOFade(pachingMaxAlpha, pachingFadeInDuration));
            }

            // 3. [Time: 회전이 끝나갈 무렵] Line Close & BG / Paching Fade Out
            float closeStartTime = pachingStartTime + (pachingRotateDuration * 0.8f); 
            
            masterSeq.Insert(closeStartTime, rect_Line.DOSizeDelta(new Vector2(rect_Line.sizeDelta.x, 0), lineCloseDuration).SetEase(Ease.InCubic));
            masterSeq.Insert(closeStartTime, img_Background.DOFade(0f, lineCloseDuration));
            
            foreach (var pachingImg in img_Pachings)
            {
                if (pachingImg == null) continue;
                masterSeq.Insert(closeStartTime, pachingImg.DOFade(0f, pachingFadeOutDuration));
            }

            // 마스터 시퀀스가 전부 끝날 때까지 한 번만 Await 처리
            await masterSeq.ToUniTask(cancellationToken: ct);
            
            // Cleanup
            img_ChiefIcon.gameObject.SetActive(false);
            img_Background.gameObject.SetActive(false);
            rect_Paching.gameObject.SetActive(false);
        }
    }
}
