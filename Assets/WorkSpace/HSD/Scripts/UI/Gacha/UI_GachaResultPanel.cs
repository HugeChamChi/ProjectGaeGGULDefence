using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class GachaResultList : UI_ListBase<IItemData, UI_ItemSlot> { }

public class UI_GachaResultPanel : UI_Base
{
    [Header("General Settings")]
    [SerializeField] private Transform resultArea;
    [SerializeField] private GachaResultList resultList;
    [SerializeField] private float animationInterval = 0.05f;

    [Header("Slot Animation Settings")]
    [SerializeField] private Vector2 slotStartScale = Vector2.zero;
    [SerializeField] private AnimationCurve appearCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float animationDuration = 0.3f;

    [Header("OpenAnimation")]
    [SerializeField] private Vector2 openStartScale = new Vector2(1f, 0.7f);
    [SerializeField] private float openAnimationDuration = 0.2f;
    [SerializeField] private AnimationCurve openEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("CloseAnimation")]
    [SerializeField] private Vector2 closeEndScale = new Vector2(1f, 0.2f);
    [SerializeField] private float closeAnimationDuration = 0.2f;
    [SerializeField] private AnimationCurve closeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Components")]
    [SerializeField] private ScrollRect scroll;
    
    private Sequence appearSequence;

    public async UniTask SetupAsync(IItemData[] items)
    {
        Open();
        if (items == null) return;

        // 1. 리스트 셋업 (생성/재사용 로직은 부모가 처리)
        resultList.Render(items);
        
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;

        // 2. 이전 연출 정리 및 초기 상태 설정
        appearSequence?.Kill();
        appearSequence = DOTween.Sequence();

        var activeSlots = resultList.GetActiveSlots();
        foreach (var slot in activeSlots)
        {
            slot.transform.localScale = slotStartScale;
            slot.transform.DOKill();
        }

        // 3. 연출 구성
        for (int i = 0; i < activeSlots.Count; i++)
        {
            var slot = activeSlots[i];
            float startTime = i * animationInterval;

            appearSequence.Insert(startTime,
                slot.transform.DOScale(Vector3.one, animationDuration)
                    .SetEase(appearCurve)).ToUniTask().Forget();
        }

        await appearSequence.Play().ToUniTask();
    }

    public override void Close()
    {
        base.Close();
        appearSequence?.Kill();
        // 개별 슬롯의 뒷정리는 다음 Setup 시 UI_ListBase가 처리하므로 최소화
    }

    public void SkipAnimation() => appearSequence?.Complete();

    protected override async UniTask OpenAnimationAsync()
    {
        resultArea.localScale = openStartScale;
        if (btn_BackgroundClose != null) btn_BackgroundClose.gameObject.SetActive(true);

        await resultArea.DOScale(Vector3.one, openAnimationDuration).SetEase(openEase).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        await resultArea.DOScale(closeEndScale, closeAnimationDuration).SetEase(closeEase).ToUniTask();
        if (btn_BackgroundClose != null) btn_BackgroundClose.gameObject.SetActive(false);
    }
}
