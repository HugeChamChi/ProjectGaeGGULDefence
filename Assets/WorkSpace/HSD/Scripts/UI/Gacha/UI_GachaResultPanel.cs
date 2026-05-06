using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UI_GachaResultPanel : UI_Base
{
    [Header("General Settings")]
    [SerializeField] private Transform resultArea;
    [SerializeField] private Button backgroundCloseButton;
    [SerializeField] private UI_ItemSlot itemSlotPrefab;
    [SerializeField] private float animationInterval = 0.05f;

    [Header("Slot Animation Settings")]
    [SerializeField] private Vector2 startScale = Vector2.zero;
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
    
    private List<UI_ItemSlot> itemSlots = new List<UI_ItemSlot>();
    private Sequence appearSequence;

    private void OnEnable()
    {
        backgroundCloseButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        backgroundCloseButton.onClick.RemoveListener(Close);
    }

    public async UniTask SetupAsync(IItemData[] items)
    {
        Open();
        if (items == null) return;

        // 1. 슬롯 확보 및 초기화
        while (itemSlots.Count < items.Length) AddSlot();
        foreach (var slot in itemSlots) slot.Setup(null);

        if (scroll != null) scroll.verticalNormalizedPosition = 1f;

        // 2. 이전 연출 정리
        appearSequence?.Kill();
        appearSequence = DOTween.Sequence();

        for (int i = 0; i < items.Length; i++)
        {
            var slot = itemSlots[i];
            slot.Setup(items[i]);
            slot.gameObject.SetActive(true);
            slot.transform.localScale = startScale;
            slot.transform.DOKill();
        }

        // 2. 연출 구성 (Insert를 사용하여 타임라인을 명확히 정의)
        for (int i = 0; i < items.Length; i++)
        {
            var slot = itemSlots[i];
            float startTime = i * animationInterval;

            appearSequence.Insert(startTime,
                slot.transform.DOScale(Vector3.one, animationDuration)
                    .SetEase(appearCurve)).ToUniTask().Forget();
        }

        // 4. 연출 대기
        await appearSequence.Play().ToUniTask();
    }

    private void AddSlot()
    {
        var newSlot = RM.Instantiate(itemSlotPrefab, scroll.content, true);
        if (newSlot != null) itemSlots.Add(newSlot);
    }

    public override void Close()
    {
        base.Close();
        appearSequence?.Kill();
        foreach (var slot in itemSlots)
        {
            if (slot != null)
            {
                slot.transform.DOKill();
                slot.Setup(null);
            }
        }
    }
    public void SkipAnimation()
    {
        appearSequence?.Complete();
    }
    public void ClearAllSlots()
    {
        appearSequence?.Kill();
        foreach (var slot in itemSlots)
        {
            if (slot != null)
            {
                slot.transform.DOKill();
                RM.Destroy(slot.gameObject);
            }
        }
        itemSlots.Clear();
    }

    protected override async UniTask OpenAnimationAsync()
    {
        resultArea.localScale = openStartScale;
        backgroundCloseButton.gameObject.SetActive(true);

        await resultArea.DOScale(Vector3.one, openAnimationDuration).SetEase(openEase).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        await resultArea.DOScale(closeEndScale, closeAnimationDuration).SetEase(closeEase).ToUniTask();
        backgroundCloseButton.gameObject.SetActive(false);
    }
}
