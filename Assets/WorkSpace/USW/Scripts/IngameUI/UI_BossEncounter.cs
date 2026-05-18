using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 등장 및 교체 연출을 담당하는 UI 클래스입니다.
/// </summary>
public class UI_BossEncounter : UI_Base
{
    [Header("UI Elements - Bosses")]
    [SerializeField] private RectTransform currentBossTr; // 현재 보스 (낚아채질 대상)
    [SerializeField] private RectTransform nextBossTr;    // 다음 보스 (왼쪽에서 등장할 대상)
    [SerializeField] private Image currentBossImage;
    [SerializeField] private Image nextBossImage;

    [SerializeField] private Image tongueImage;         // 혀 몸통 (보스 뒤에 위치)
    [SerializeField] private Image tongueTip;           // 혀 끝 (보스 앞에 위치하여 샌드위치 효과)

    [Header("UI Elements - Text")]
    [SerializeField] private TMP_Text waveText;

    [Header("Animation Settings")]
    [SerializeField] private float popDuration = 0.4f;
    [SerializeField] private float snatchDuration = 0.3f;
    [SerializeField] private float entranceDuration = 0.5f;
    [SerializeField] private Vector2 tongueDefaultSize = new Vector2(0, 80);

    private Transform _originalParent;
    private Vector2 _originalAnchorMin, _originalAnchorMax, _originalPivot, _originalAnchoredPos;
    private Quaternion _originalRotation;
    private Transform _tongueTipOriginalParent;

    protected override void Awake()
    {
        base.Awake();
        Setting();

        gameObject.SetActive(false); // 초기 비활성화
        ResetElements();
    }

    private void Setting()
    {
        if (currentBossTr != null)
        {
            _originalParent = currentBossTr.parent;
            _originalAnchorMin = currentBossTr.anchorMin;
            _originalAnchorMax = currentBossTr.anchorMax;
            _originalPivot = currentBossTr.pivot;
            _originalAnchoredPos = currentBossTr.anchoredPosition;
            _originalRotation = currentBossTr.rotation;
        }
        if (tongueTip != null) _tongueTipOriginalParent = tongueTip.transform.parent;
    }

    [Button("보스 교체 연출 최종 테스트")]
    public void PlayTest()
    {
        PlayBossTransitionSequence(null, null, 1).Forget();
    }

    protected override async UniTask OpenAnimationAsync()
    {
        
    }

    protected override async UniTask CloseAnimationAsync()
    {
        // 패널 전체가 뿅 하고 작아지며 닫히는 연출
        await transform.DOScale(0f, 0.3f).SetEase(Ease.InBack).ToUniTask();
    }

    public async UniTask PlayBossTransitionSequence(Sprite currentBossIcon, Sprite nextBossIcon, int currentWave)
    {
        Open();
        gameObject.SetActive(true);
        ResetElements();

        if (currentBossImage != null) currentBossImage.sprite = currentBossIcon;
        if (nextBossImage != null) nextBossImage.sprite = nextBossIcon;

        bool hasPrevBoss = currentBossIcon != null;

        // 1. 초기 등장 연출 (원래 디자인된 상태로 뿅!)
        if (waveText != null)
        {
            // 패널 자체도 뿅 하고 나타남
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask().Forget();

            var tasks = new List<UniTask>();

            if (currentWave == 1)
            {
                waveText.alpha = 0;
                waveText.rectTransform.localScale = Vector3.zero;
            }
            else
            {
                waveText.text = $"WAVE {(currentWave - 1).ToString()}";
                waveText.alpha = 1f;
                waveText.rectTransform.localScale = Vector3.zero;
                tasks.Add(waveText.rectTransform.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask());
            }

            if (hasPrevBoss && currentBossTr != null)
            {
                currentBossTr.gameObject.SetActive(true);
                currentBossTr.localScale = Vector3.zero;
                tasks.Add(currentBossTr.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask());
            }

            if (tasks.Count > 0) await UniTask.WhenAll(tasks);
        }

        if (hasPrevBoss)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.7f), delayTiming: PlayerLoopTiming.Update);

            // 2. 낚아서 채가기 (Sandwich Grab & Pull)
            if (tongueImage != null && currentBossTr != null)
            {
                tongueImage.gameObject.SetActive(true);
                
                // 혀 조준 및 발사
                Vector3 tongueStartWorldPos = tongueImage.rectTransform.position;
                Vector3 targetWorldPos = currentBossTr.TransformPoint(currentBossTr.rect.center);
                
                Vector2 direction = tongueImage.rectTransform.parent.InverseTransformPoint(targetWorldPos) - 
                                   tongueImage.rectTransform.parent.InverseTransformPoint(tongueStartWorldPos);
                
                tongueImage.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                
                float targetDistance = direction.magnitude;
                await tongueImage.rectTransform.DOSizeDelta(new Vector2(targetDistance, tongueDefaultSize.y), 0.15f)
                    .SetEase(Ease.OutCubic).ToUniTask();

                // [핵심] 낚아채는 '그 순간'에만 연출용 세팅으로 전환
                currentBossTr.SetParent(tongueImage.rectTransform, true);
                currentBossTr.anchorMin = new Vector2(1, 0.5f); // 혀의 Pivot이 0일 때, 1이 끝부분(Tip)입니다.
                currentBossTr.anchorMax = new Vector2(1, 0.5f);
                currentBossTr.pivot = new Vector2(0.5f, 0.5f);
                currentBossTr.anchoredPosition = Vector2.zero; // 혀 끝에 밀착
                
                if (tongueTip != null)
                {
                    tongueTip.gameObject.SetActive(true);
                    tongueTip.transform.SetParent(currentBossTr, false);
                    tongueTip.rectTransform.anchoredPosition = Vector2.zero;
                    tongueTip.rectTransform.localScale = Vector3.one;
                }

                // 낚아채기 충격 (Squash)
                currentBossTr.DOPunchScale(new Vector3(-0.2f, 0.2f, 0), 0.1f).ToUniTask().Forget();

                // 혀 회수 (보스가 혀 끝에 완전히 고정되어 프레임 오차 없이 빨려 들어감)
                Sequence pullSeq = DOTween.Sequence()
                .Join(tongueImage.rectTransform.DOSizeDelta(new Vector2(0, tongueDefaultSize.y), snatchDuration).SetEase(Ease.InBack))
                .Join(currentBossTr.DORotate(new Vector3(0, 0, 90f), snatchDuration).SetEase(Ease.InBack));
                
                await pullSeq.Play().ToUniTask();
                
                currentBossTr.gameObject.SetActive(false);
                tongueImage.gameObject.SetActive(false);
                if (tongueTip != null) tongueTip.gameObject.SetActive(false);

                // 부모 및 레이아웃 복구
                RestoreBossLayout();
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.3f), delayTiming: PlayerLoopTiming.Update);
        }
        else
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), delayTiming: PlayerLoopTiming.Update);
        }

        // 3. 다음 보스 등장 (왼쪽 -> 중앙) & WAVE 업데이트
        if (nextBossTr != null)
        {
            nextBossTr.gameObject.SetActive(true);
            nextBossTr.anchoredPosition = new Vector2(-1200, 0); 
            nextBossTr.localScale = Vector3.one;

            Sequence transitionSeq = DOTween.Sequence()
            .Join(nextBossTr.DOAnchorPos(Vector2.zero, entranceDuration).SetEase(Ease.OutBack));

            if (waveText != null)
            {
                if (currentWave == 1)
                {
                    transitionSeq.AppendCallback(() => {
                        waveText.text = "WAVE 1";
                        waveText.alpha = 1f;
                    })
                    .Join(waveText.rectTransform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
                }
                else
                {
                    transitionSeq.AppendCallback(() => waveText.text = $"WAVE {currentWave.ToString()}")
                    .Join(waveText.rectTransform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 10, 1f));
                }
            }

            await transitionSeq.Play().ToUniTask();
        }

        // 4. 연출 종료 및 전체 패널 닫기
        await UniTask.Delay(TimeSpan.FromSeconds(1.2f), delayTiming: PlayerLoopTiming.Update);
        await CloseAsync(); 
    }

    private void ResetElements()
    {
        transform.localScale = Vector3.one;
        transform.DOKill();
        RestoreBossLayout();

        if (nextBossTr != null)
        {
            nextBossTr.anchoredPosition = new Vector2(-1200, 0);
            nextBossTr.localScale = Vector3.one;
            nextBossTr.rotation = Quaternion.identity;
            nextBossTr.gameObject.SetActive(false);
            nextBossTr.DOKill();
        }

        if (tongueImage != null)
        {
            tongueImage.rectTransform.sizeDelta = new Vector2(0, tongueDefaultSize.y);
            tongueImage.gameObject.SetActive(false);
        }

        if (tongueTip != null)
        {
            tongueTip.transform.SetParent(_tongueTipOriginalParent, false);
            tongueTip.gameObject.SetActive(false);
        }

        if (waveText != null)
        {
            waveText.alpha = 0;
            waveText.rectTransform.localScale = Vector3.one;
            waveText.DOKill();
        }
    }

    private void RestoreBossLayout()
    {
        if (currentBossTr != null)
        {
            currentBossTr.SetParent(_originalParent, false);
            currentBossTr.anchorMin = _originalAnchorMin;
            currentBossTr.anchorMax = _originalAnchorMax;
            currentBossTr.pivot = _originalPivot;
            currentBossTr.anchoredPosition = _originalAnchoredPos;
            currentBossTr.rotation = _originalRotation;
            currentBossTr.localScale = Vector3.one;
            currentBossTr.gameObject.SetActive(false);
            currentBossTr.DOKill();
        }
    }
}
