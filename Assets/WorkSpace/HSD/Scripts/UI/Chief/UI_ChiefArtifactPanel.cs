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
        
        // 고정된 함수 참조로 연결하여 GC Alloc 방지
        if (btn_ChiefTab != null) btn_ChiefTab.onClick.AddListener(OnChiefTabClicked);
        if (btn_ArtifactTab != null) btn_ArtifactTab.onClick.AddListener(OnArtifactTabClicked);
        if (btn_Apply != null) btn_Apply.onClick.AddListener(_presenter.OnApplyClicked);
        if (btn_Close != null) btn_Close.onClick.AddListener(Close);
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
        if(obj_ChiefGroup != null && obj_ChiefGroup.activeSelf != isChiefTab) 
            obj_ChiefGroup.SetActive(isChiefTab);
        
        if(obj_ArtifactGroup != null && obj_ArtifactGroup.activeSelf == isChiefTab) 
            obj_ArtifactGroup.SetActive(!isChiefTab);
    }

    public void SetupChiefList(IEnumerable<ChiefData> chiefs, System.Action<ChiefData> onSelect)
    {
        int index = 0;
        foreach (var data in chiefs)
        {
            UI_ChiefSlot slot;
            if (index < _chiefSlots.Count)
            {
                // 1. 재사용 (Reuse)
                slot = _chiefSlots[index];
                slot.gameObject.SetActive(true);
            }
            else
            {
                // 2. 풀링 기반 생성 (Pooling)
                slot = RM.Instantiate(slotPrefab.gameObject, tr_ChiefContent, isPool: true).GetComponent<UI_ChiefSlot>();
                _chiefSlots.Add(slot);
            }
            
            slot.SetData(data, onSelect);
            index++;
        }

        // 사용하지 않는 남은 슬롯들은 비활성화 (Destroy 대신 재사용 대기)
        for (int i = index; i < _chiefSlots.Count; i++)
        {
            if(_chiefSlots[i].gameObject.activeSelf)
                _chiefSlots[i].gameObject.SetActive(false);
        }
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
        for (int i = 0; i < _chiefSlots.Count; i++)
        {
            if (!_chiefSlots[i].gameObject.activeSelf) continue;
            _chiefSlots[i].SetSelected(_chiefSlots[i].Data.Id == selectedId);
        }
    }
}
