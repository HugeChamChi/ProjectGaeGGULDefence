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
    [SerializeField] private Image tongueImage;

    [Header("UI Elements - Text")]
    [SerializeField] private TMP_Text waveText;

    [Header("Animation Settings")]
    [SerializeField] private float popDuration = 0.4f;
    [SerializeField] private float snatchDuration = 0.3f;
    [SerializeField] private float entranceDuration = 0.5f;
    [SerializeField] private Vector2 tongueDefaultSize = new Vector2(0, 80);

    private Transform _currentBossOriginalParent;

    private void Awake()
    {
        if (currentBoss != null) _currentBossOriginalParent = currentBoss.parent;
        gameObject.SetActive(false); // 초기 비활성화
        ResetElements();
    }

    [Button("보스 교체 연출 최종 테스트")]
    public void PlayTest()
    {
        Open();
    }

    protected override async UniTask OpenAnimationAsync()
    {
        await PlayBossTransitionSequence();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        // 패널 전체가 뿅 하고 작아지며 닫히는 연출
        await transform.DOScale(0f, 0.3f).SetEase(Ease.InBack).ToUniTask();
    }

    private async UniTask PlayBossTransitionSequence()
    {
        ResetElements();

        // 1. 초기 등장 연출
        if (waveText != null && currentBoss != null)
        {
            // 패널 자체도 뿅 하고 나타남
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask().Forget();

            waveText.text = "WAVE 1";
            waveText.alpha = 1f;
            waveText.rectTransform.localScale = Vector3.zero;
            
            currentBoss.gameObject.SetActive(true);
            currentBoss.anchoredPosition = Vector2.zero;
            currentBoss.localScale = Vector3.zero;

            await UniTask.WhenAll(
                waveText.rectTransform.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask(),
                currentBoss.DOScale(1f, popDuration).SetEase(Ease.OutBack).ToUniTask()
            );
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.7f));

        // 2. 낚아서 채가기 (Grab & Pull)
        if (tongueImage != null && currentBoss != null)
        {
            tongueImage.gameObject.SetActive(true);
            
            // 혀 조준 및 발사
            Vector3 tongueStartWorldPos = tongueImage.rectTransform.position;
            
            // 보스의 Pivot 위치와 상관없이 Rect의 정중앙 월드 좌표를 계산하여 조준
            Vector3 targetWorldPos = currentBoss.TransformPoint(currentBoss.rect.center);
            
            Vector2 direction = tongueImage.rectTransform.parent.InverseTransformPoint(targetWorldPos) - 
                               tongueImage.rectTransform.parent.InverseTransformPoint(tongueStartWorldPos);
            
            tongueImage.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            
            float targetDistance = direction.magnitude;
            await tongueImage.rectTransform.DOSizeDelta(new Vector2(targetDistance, tongueDefaultSize.y), 0.15f)
                .SetEase(Ease.OutCubic).ToUniTask();

            // 보스를 혀의 자식으로 편입하여 물리적으로 함께 움직이게 함
            currentBoss.SetParent(tongueImage.rectTransform, true);
            
            // 낚아채기 충격
            currentBoss.DOPunchScale(new Vector3(-0.2f, 0.2f, 0), 0.1f).ToUniTask().Forget();

            // 혀가 감기면서 보스를 완벽하게 끌고 감
            // 보스가 자식이므로 혀의 Width만 줄여도 자동으로 끌려옴
            await tongueImage.rectTransform.DOSizeDelta(new Vector2(0, tongueDefaultSize.y), snatchDuration)
                .SetEase(Ease.InBack).ToUniTask();
            
            currentBoss.gameObject.SetActive(false);
            tongueImage.gameObject.SetActive(false);

            // 보스 부모 복구
            currentBoss.SetParent(_currentBossOriginalParent, false);
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.3f));

        // 3. 다음 보스 등장 (왼쪽 -> 중앙) & WAVE 2 업데이트
        if (nextBoss != null)
        {
            nextBoss.gameObject.SetActive(true);
            nextBoss.anchoredPosition = new Vector2(-1200, 0); // 왼쪽 멀리서 대기
            nextBoss.localScale = Vector3.one;

            Sequence transitionSeq = DOTween.Sequence();
            
            // 왼쪽에서 중앙으로 슬라이드 인
            transitionSeq.Join(nextBoss.DOAnchorPos(Vector2.zero, entranceDuration).SetEase(Ease.OutBack));

            // WaveText 업데이트
            if (waveText != null)
            {
                transitionSeq.AppendCallback(() => waveText.text = "WAVE 2");
                transitionSeq.Join(waveText.rectTransform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 10, 1f));
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

        if (currentBoss != null)
        {
            currentBoss.SetParent(_currentBossOriginalParent, false);
            currentBoss.anchoredPosition = Vector2.zero;
            currentBoss.localScale = Vector3.one;
            currentBoss.rotation = Quaternion.identity;
            currentBoss.gameObject.SetActive(false);
            currentBoss.DOKill();
        }

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

        if (waveText != null)
        {
            waveText.alpha = 0;
            waveText.rectTransform.localScale = Vector3.one;
            waveText.DOKill();
        }
    }
}
