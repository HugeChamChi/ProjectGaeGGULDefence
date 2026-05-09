using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;

public class UI_ChiefArtifactPresenter
{
    private readonly UI_ChiefArtifactPanel _view;
    
    private ChiefData _currentAppliedChief;
    private ChiefData _selectedChief;
    
    // 콜백 델리게이트 캐싱 (이벤트 할당 GC 최적화)
    private Action<ChiefData> _onChiefSlotClickedDelegate;

    public UI_ChiefArtifactPresenter(UI_ChiefArtifactPanel view)
    {
        _view = view;
        _onChiefSlotClickedDelegate = OnChiefSlotClicked;
    }

    public async UniTask Initialize()
    {
        // 1. 현재 적용된 족장 데이터 로드
        int currentId = Player.Chief.SelectedChiefId;
        _currentAppliedChief = Table.Character.Chief.GetChief(currentId);
        
        // 데이터가 없는 경우 첫 번째 족장을 기본값으로 설정
        if (_currentAppliedChief == null)
        {
            _currentAppliedChief = Table.Character.Chief.Chiefs.FirstOrDefault();
        }

        _selectedChief = _currentAppliedChief;

        // 2. UI 초기화
        _view.UpdateTabUI(true); // 기본적으로 족장 탭
        
        // 캐싱된 델리게이트 전달하여 슬롯 리스트 구성
        _view.SetupChiefList(Table.Character.Chief.Chiefs, _onChiefSlotClickedDelegate);
        
        RefreshUI();
        
        await UniTask.CompletedTask;
    }

    public void OnTabChanged(bool isChiefTab)
    {
        _view.UpdateTabUI(isChiefTab);
        
        if (!isChiefTab)
        {
            // TODO: 유물 리스트 초기화 로직 (현재는 데이터 없음)
        }
    }

    private void OnChiefSlotClicked(ChiefData data)
    {
        if (data == null || _selectedChief == data) return;
        
        _selectedChief = data;
        RefreshUI();
    }

    public void OnApplyClicked()
    {
        if (_selectedChief == null || _selectedChief == _currentAppliedChief) return;
        
        // 실제 데이터에 적용 및 서버 저장
        _currentAppliedChief = _selectedChief;
        Player.Chief.SetSelectedChief(_currentAppliedChief.Id);
        
        RefreshUI();
        
        UnityEngine.Debug.Log($"Applied Chief: {_currentAppliedChief.Name}");
    }

    private void RefreshUI()
    {
        _view.UpdatePreview(_selectedChief);
        
        // 현재 선택된(임시) 족장 하이라이트
        if (_selectedChief != null)
        {
            _view.UpdateSlotSelection(_selectedChief.Id);
        }
    }
}
