using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class GachaChanceList : UI_ListBase<GachaChanceData, UI_GachaChanceSlot> { }

public class UI_GachaChancePanel : UI_Base
{
    [Header("UI")]
    [SerializeField] private GachaChanceList chanceList;

    [Header("Animations")]
    [SerializeField] private Vector2 startScale = new Vector2(0.8f, 0.8f);
    [SerializeField] private float duration = 0.2f;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        var items = Table.Gacha.CharacterGacha.ProbabilityItems;
        if (items == null) return;

        // 신규 데이터 구조에 맞춰 변환 후 리스트 셋업
        List<GachaChanceData> dataList = new List<GachaChanceData>();
        foreach (var item in items)
        {
            dataList.Add(new GachaChanceData { rarity = item.itemName, percent = item.percent });
        }

        chanceList.Setup(dataList);
    }

    protected override async UniTask OpenAnimationAsync()
    {
        if (btn_BackgroundClose != null) btn_BackgroundClose.gameObject.SetActive(true);
        transform.localScale = startScale;
        await transform.DOScale(Vector3.one, duration).SetEase(Ease.OutBack).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        await transform.DOScale(startScale, duration).SetEase(Ease.InBack).ToUniTask();
        if (btn_BackgroundClose != null) btn_BackgroundClose.gameObject.SetActive(false);
    }
}
