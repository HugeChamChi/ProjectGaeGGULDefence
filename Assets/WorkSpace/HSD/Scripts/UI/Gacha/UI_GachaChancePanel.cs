using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가챠 확률 정보를 보여주는 팝업 패널입니다.
/// </summary>
public class UI_GachaChancePanel : UI_Base
{
    [Header("UI")]
    [SerializeField] private Button backgroundCloseButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform slotParent; // Vertical Layout Group이 있는 부모
    [SerializeField] private UI_GachaChanceSlot slotPrefab;

    [Header("Animations")]
    [SerializeField] private Vector2 startScale = new Vector2(0.8f, 0.8f);
    [SerializeField] private float duration = 0.2f;

    private List<UI_GachaChanceSlot> _slots = new List<UI_GachaChanceSlot>();

    private void OnEnable()
    {
        closeButton.onClick.AddListener(Close);
        Refresh();
    }

    private void OnDisable()
    {
        closeButton.onClick.RemoveListener(Close);
    }

    public void Refresh()
    {
        // 1. 이미 슬롯이 생성되어 있다면 초기화를 건너뜀 (GC 최적화)
        if (_slots.Count > 0) return;

        // 2. 가챠 시스템에서 확률 데이터 가져오기
        var items = Table.Gacha.CharacterGacha.ProbabilityItems;
        if (items == null) return;

        // 3. 슬롯 생성 및 배치
        foreach (var item in items)
        {
            UI_GachaChanceSlot slot = Instantiate(slotPrefab, slotParent);
            slot.Setup(item.itemName, item.percent);
            _slots.Add(slot);
        }
    }

    protected override async UniTask OpenAnimationAsync()
    {
        if (backgroundCloseButton != null) backgroundCloseButton.gameObject.SetActive(true);
        transform.localScale = startScale;
        await transform.DOScale(Vector3.one, duration).SetEase(Ease.OutBack).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        await transform.DOScale(startScale, duration).SetEase(Ease.InBack).ToUniTask();
        if (backgroundCloseButton != null) backgroundCloseButton.gameObject.SetActive(false);
    }
}
