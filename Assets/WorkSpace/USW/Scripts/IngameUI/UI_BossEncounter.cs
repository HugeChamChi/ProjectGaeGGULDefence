using System;
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
    [SerializeField] private RectTransform currentBoss; // 현재 보스 (낚아채질 대상)
    [SerializeField] private RectTransform nextBoss;    // 다음 보스 (왼쪽에서 등장할 대상)
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
        if (currentBoss != null)
        {
            _originalParent = currentBoss.parent;
            _originalAnchorMin = currentBoss.anchorMin;
            _originalAnchorMax = currentBoss.anchorMax;
            _originalPivot = currentBoss.pivot;
            _originalAnchoredPos = currentBoss.anchoredPosition;
            _originalRotation = currentBoss.rotation;
        }
        if (tongueTip != null) _tongueTipOriginalParent = tongueTip.transform.parent;
    }

    [Button("보스 교체 연출 최종 테스트")]
    public void PlayTest()
    {
        PlayBossTransitionSequence(1).Forget();
    }

    protected override async UniTask OpenAnimationAsync()
    {
        
    }

    protected override async UniTask CloseAnimationAsync()
    {
        // 패널 전체가 뿅 하고 작아지며 닫히는 연출
        await transform.DOScale(0f, 0.3f).SetEase(Ease.InBack).ToUniTask();
    }

    public async UniTask PlayBossTransitionSequence(int currentWave)
    {
        Open();
        ResetElements();

        // 1. 초기 등장 연출 (원래 디자인된 상태로 뿅!)
        if (waveText != null && currentBoss != null)
        {
            // 패널 자체도 뿅 하고 나타남
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask().Forget();

            waveText.text = $"WAVE {currentWave.ToString()}";
            waveText.alpha = 1f;
            waveText.rectTransform.localScale = Vector3.zero;
            
            currentBoss.gameObject.SetActive(true);
            currentBoss.localScale = Vector3.zero;

            await UniTask.WhenAll(
                waveText.rectTransform.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask(),
                currentBoss.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask()
            );
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.7f));

        // 2. 낚아서 채가기 (Sandwich Grab & Pull)
        if (tongueImage != null && currentBoss != null)
        {
            tongueImage.gameObject.SetActive(true);
            
            // 혀 조준 및 발사
            Vector3 tongueStartWorldPos = tongueImage.rectTransform.position;
            Vector3 targetWorldPos = currentBoss.TransformPoint(currentBoss.rect.center);
            
            Vector2 direction = tongueImage.rectTransform.parent.InverseTransformPoint(targetWorldPos) - 
                               tongueImage.rectTransform.parent.InverseTransformPoint(tongueStartWorldPos);
            
            tongueImage.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            
            float targetDistance = direction.magnitude;
            await tongueImage.rectTransform.DOSizeDelta(new Vector2(targetDistance, tongueDefaultSize.y), 0.15f)
                .SetEase(Ease.OutCubic).ToUniTask();

            // [핵심] 낚아채는 '그 순간'에만 연출용 세팅으로 전환
            currentBoss.SetParent(tongueImage.rectTransform, true);
            currentBoss.anchorMin = new Vector2(1, 0.5f); // 혀의 Pivot이 0일 때, 1이 끝부분(Tip)입니다.
            currentBoss.anchorMax = new Vector2(1, 0.5f);
            currentBoss.pivot = new Vector2(0.5f, 0.5f);
            currentBoss.anchoredPosition = Vector2.zero; // 혀 끝에 밀착
            
            if (tongueTip != null)
            {
                tongueTip.gameObject.SetActive(true);
                tongueTip.transform.SetParent(currentBoss, false);
                tongueTip.rectTransform.anchoredPosition = Vector2.zero;
                tongueTip.rectTransform.localScale = Vector3.one;
            }

            // 낚아채기 충격 (Squash)
            currentBoss.DOPunchScale(new Vector3(-0.2f, 0.2f, 0), 0.1f).ToUniTask().Forget();

            // 혀 회수 (보스가 혀 끝에 완전히 고정되어 프레임 오차 없이 빨려 들어감)
            Sequence pullSeq = DOTween.Sequence()
            .Join(tongueImage.rectTransform.DOSizeDelta(new Vector2(0, tongueDefaultSize.y), snatchDuration).SetEase(Ease.InBack))
            .Join(currentBoss.DORotate(new Vector3(0, 0, 90f), snatchDuration).SetEase(Ease.InBack));
            
            await pullSeq.Play().ToUniTask();
            
            currentBoss.gameObject.SetActive(false);
            tongueImage.gameObject.SetActive(false);
            if (tongueTip != null) tongueTip.gameObject.SetActive(false);

            // 부모 및 레이아웃 복구
            RestoreBossLayout();
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

        // 3. 다음 보스 등장 (왼쪽 -> 중앙) & WAVE 2 업데이트
        if (nextBoss != null)
        {
            nextBoss.gameObject.SetActive(true);
            nextBoss.anchoredPosition = new Vector2(-1200, 0); 
            nextBoss.localScale = Vector3.one;

            Sequence transitionSeq = DOTween.Sequence()
            .Join(nextBoss.DOAnchorPos(Vector2.zero, entranceDuration).SetEase(Ease.OutBack));

            if (waveText != null)
            {
                transitionSeq.AppendCallback(() => waveText.text = $"WAVE {(currentWave + 1).ToString()}")
                .Join(waveText.rectTransform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 10, 1f)).ToUniTask().Forget();
            }

            await transitionSeq.Play().ToUniTask();
        }

        // 4. 연출 종료 및 전체 패널 닫기
        await UniTask.Delay(TimeSpan.FromSeconds(1.2f));
        await CloseAsync(); 
    }

    private void ResetElements()
    {
        transform.localScale = Vector3.one;
        transform.DOKill();
        RestoreBossLayout();

        if (nextBoss != null)
        {
            nextBoss.anchoredPosition = new Vector2(-1200, 0);
            nextBoss.localScale = Vector3.one;
            nextBoss.rotation = Quaternion.identity;
            nextBoss.gameObject.SetActive(false);
            nextBoss.DOKill();
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
        if (currentBoss != null)
        {
            currentBoss.SetParent(_originalParent, false);
            currentBoss.anchorMin = _originalAnchorMin;
            currentBoss.anchorMax = _originalAnchorMax;
            currentBoss.pivot = _originalPivot;
            currentBoss.anchoredPosition = _originalAnchoredPos;
            currentBoss.rotation = _originalRotation;
            currentBoss.localScale = Vector3.one;
            currentBoss.gameObject.SetActive(false);
            currentBoss.DOKill();
        }
    }
}
