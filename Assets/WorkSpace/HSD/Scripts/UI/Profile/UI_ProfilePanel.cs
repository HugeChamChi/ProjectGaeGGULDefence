using UnityEngine;

public class UI_ProfilePanel : UI_ProfilePanelBase
{
    [Header("Components")]
    [SerializeField] UI_ProfileChangePanel UI_ProfileChangePanel;

    protected override void OnEnable()
    {
        base.OnEnable();
        Setup();
    }

    private void Setup()
    {
        ui_ProfileIconSlot.SetButtonEvent(() => UI_ProfileChangePanel.gameObject.SetActive(true));
        playerName.text = User.PlayerData.Data.PlayerName;
    }
}