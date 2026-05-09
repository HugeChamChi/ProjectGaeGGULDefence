using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class UI_ChiefArtifactPanel : UI_Base
{
    [Header("Tabs")]
    [SerializeField] Button btn_ChiefTab;
    [SerializeField] Button btn_ArtifactTab;

    [Header("Lists")]
    [SerializeField] GameObject obj_ChiefGroup;
    [SerializeField] GameObject obj_ArtifactGroup;
    [SerializeField] Transform tr_ChiefContent;
    [SerializeField] Transform tr_ArtifactContent;

    [Header("Preview")]
    [SerializeField] Image img_PreviewChief;
    [SerializeField] Button btn_Apply;
    [SerializeField] Button btn_Close;

    [Header("Prefabs")]
    [SerializeField] UI_ChiefSlot slotPrefab;

    private List<UI_ChiefSlot> _chiefSlots = new List<UI_ChiefSlot>();
    private UI_ChiefArtifactPresenter _presenter;

    private void Awake()
    {
        _presenter = new UI_ChiefArtifactPresenter(this);
        
        btn_ChiefTab.onClick.AddListener(() => _presenter.OnTabChanged(true));
        btn_ArtifactTab.onClick.AddListener(() => _presenter.OnTabChanged(false));
        btn_Apply.onClick.AddListener(() => _presenter.OnApplyClicked());
        btn_Close.onClick.AddListener(Close);
    }

    public override async UniTask OpenAsync()
    {
        await base.OpenAsync();
        await _presenter.Initialize();
    }

    public void UpdateTabUI(bool isChiefTab)
    {
        obj_ChiefGroup.SetActive(isChiefTab);
        obj_ArtifactGroup.SetActive(!isChiefTab);
    }

    public void SetupChiefList(IEnumerable<ChiefData> chiefs, System.Action<ChiefData> onSelect)
    {
        // 기존 슬롯 제거 또는 풀링 처리 (여기선 RM.Instantiate 활용 가정)
        foreach (var slot in _chiefSlots)
        {
            RM.Destroy(slot.gameObject);
        }
        _chiefSlots.Clear();

        foreach (var data in chiefs)
        {
            var slot = RM.Instantiate(slotPrefab.gameObject, tr_ChiefContent).GetComponent<UI_ChiefSlot>();
            slot.SetData(data, onSelect);
            _chiefSlots.Add(slot);
        }
    }

    public void UpdatePreview(ChiefData data)
    {
        if (data != null && img_PreviewChief != null)
        {
            img_PreviewChief.sprite = data.Icon;
            img_PreviewChief.gameObject.SetActive(true);
        }
    }

    public void UpdateSlotSelection(int selectedId)
    {
        foreach (var slot in _chiefSlots)
        {
            slot.SetSelected(slot.Data.Id == selectedId);
        }
    }
}
