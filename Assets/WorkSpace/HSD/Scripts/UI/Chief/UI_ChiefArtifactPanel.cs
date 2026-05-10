using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

[System.Serializable]
public class ChiefList : UI_ListBase<ChiefData, UI_ChiefSlot> { }

public class UI_ChiefArtifactPanel : UI_Base
{
    [Header("Tabs")]
    [SerializeField] Button btn_ChiefTab;
    [SerializeField] Button btn_ArtifactTab;

    [Header("Lists")]
    [SerializeField] ChiefList chiefList;
    [SerializeField] GameObject obj_ArtifactGroup; // 얘는 아직 List가 아니니 그대로 둠

    [Header("Preview")]
    [SerializeField] Image img_PreviewChief;
    [SerializeField] Button btn_Apply;

    private UI_ChiefArtifactPresenter _presenter;

    protected override void Awake()
    {
        base.Awake();
        _presenter = new UI_ChiefArtifactPresenter(this);
        
        if (btn_ChiefTab != null) btn_ChiefTab.onClick.AddListener(OnChiefTabClicked);
        if (btn_ArtifactTab != null) btn_ArtifactTab.onClick.AddListener(OnArtifactTabClicked);
        if (btn_Apply != null) btn_Apply.onClick.AddListener(_presenter.OnApplyClicked);
    }

    private void OnChiefTabClicked() => _presenter.OnTabChanged(true);
    private void OnArtifactTabClicked() => _presenter.OnTabChanged(false);

    public override async UniTask OpenAsync()
    {
        await base.OpenAsync();
        await _presenter.Initialize();
    }

    public void UpdateTabUI(bool isChiefTab)
    {
        chiefList.SetActive(isChiefTab);
        
        if(obj_ArtifactGroup != null && obj_ArtifactGroup.activeSelf == isChiefTab) 
            obj_ArtifactGroup.SetActive(!isChiefTab);
    }

    public void SetupChiefList(IEnumerable<ChiefData> chiefs, System.Action<ChiefData> onSelect)
    {
        chiefList.Render(chiefs, (data, slot) => 
        {
            slot.SetCallback(onSelect);
        });
    }

    public void UpdatePreview(ChiefData data)
    {
        if (data != null && img_PreviewChief != null)
        {
            img_PreviewChief.sprite = data.Icon;
            if(!img_PreviewChief.gameObject.activeSelf) 
                img_PreviewChief.gameObject.SetActive(true);
        }
    }

    public void UpdateSlotSelection(int selectedId)
    {
        foreach (var slot in chiefList.GetActiveSlots())
        {
            slot.SetSelected(slot.Data.Id == selectedId);
        }
    }
}
