using Cysharp.Threading.Tasks;
using GaeGGUL.Animation;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class GachaResultList : UI_ListBase<IItemData, UI_ItemSlot> { }

/// <summary>
/// 가차 결과 화면을 표시하는 패널입니다.
/// </summary>
public class UI_GachaResultPanel : UI_Base
{
    [Header("General Settings")]
    [SerializeField] private GachaResultList resultList;

    [Header("Animations")]
    [SerializeField] private Anim_ListStaggered listStaggered;

    [Header("Components")]
    [SerializeField] private ScrollRect scroll;

    public async UniTask SetupAsync(IItemData[] items)
    {
        Open();
        if (items == null) return;

        // 1. 리스트 셋업 (UI_ListBase가 생성 및 바인딩 처리)
        resultList.Render(items);
        
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;

        // 2. 리스트 연출 재생
        var activeSlots = resultList.GetActiveSlots();
        if (listStaggered != null)
        {
            await listStaggered.PlayInAsync(activeSlots);
        }
    }

    public override void Close()
    {
        base.Close();
        listStaggered?.Skip(); // 닫을 때 진행 중인 리스트 연출 중단
    }

    /// <summary>
    /// 현재 진행 중인 리스트 등장 연출을 스킵합니다.
    /// </summary>
    public void SkipAnimation() => listStaggered?.Skip();

    // 패널 자체의 등장/퇴장 연출은 부모 클래스(UI_Base)의 targetAnim을 통해 처리됩니다.
    // 인스펙터에서 targetAnim에 Anim_UI_Scale(대상: resultArea)을 할당하여 사용하세요.
}
