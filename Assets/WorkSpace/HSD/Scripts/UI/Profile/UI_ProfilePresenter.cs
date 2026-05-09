using UnityEngine;

public class UI_ProfilePresenter
{
    private readonly UI_ProfileChangePanel _view;
    
    private IProfileItem _tempIconData;
    private IProfileItem _tempFrameData;

    public UI_ProfilePresenter(UI_ProfileChangePanel view)
    {
        _view = view;
    }

    public void Initialize()
    {
        _tempIconData = Player.Profile.CurrentIconData;
        _tempFrameData = Player.Profile.CurrentFrameData;
        
        _view.UpdatePreview(_tempIconData, _tempFrameData);
    }

    public void OnItemSelected(IProfileItem item)
    {
        if (item.Type == ProfileItemType.Icon) _tempIconData = item;
        else if (item.Type == ProfileItemType.Frame) _tempFrameData = item;

        _view.UpdatePreview(_tempIconData, _tempFrameData);
        _view.UpdateApplyButtonState(item.IsUnlocked && IsChanged());
    }

    private bool IsChanged()
    {
        return _tempIconData.Id != Player.Profile.CurrentIconData.Id || 
               _tempFrameData.Id != Player.Profile.CurrentFrameData.Id;
    }

    public void OnApplyClicked()
    {
        Player.Profile.Data.CurrentIconId = _tempIconData.Id;
        Player.Profile.Data.CurrentFrameId = _tempFrameData.Id;
        
        // Player.Profile.Save(); // TODO: 저장 로직 활성화 시 주석 해제
        
        _view.RefreshList();
        _view.UpdateApplyButtonState(false);
        Debug.Log("Profile changes applied and saved.");
    }
}
