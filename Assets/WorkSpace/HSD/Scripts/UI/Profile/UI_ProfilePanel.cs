using UnityEngine;

public class UI_ProfilePanel : UI_ProfilePanelBase
{
    [Header("Components")]
    [SerializeField] UI_ProfileChangePanel UI_ProfileChangePanel;


    public void Setup(PlayerData data)
    {
        UI_ProfileIconSlot.Setup(null, () => UI_ProfileChangePanel.gameObject.SetActive(true));
        playerName.text = data.PlayerName;
    }
}