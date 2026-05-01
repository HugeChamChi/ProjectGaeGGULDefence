using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class UI_ProfilePanel : UI_ProfilePanelBase
{
    [Header("Components")]
    [SerializeField] Transform panel;
    [SerializeField, FormerlySerializedAs("UI_ProfileChangePanel")] 
    UI_ProfileChangePanel ui_ProfileChangePanel;

    protected override void OnEnable()
    {
        base.OnEnable();
        Setup();
    }

    private void Setup()
    {
        ui_ProfileIconSlot.SetButtonEvent(() => ui_ProfileChangePanel.gameObject.SetActive(true));
        playerName.text = User.PlayerData.Data.PlayerName;
    }

    public override async UniTask OpenAsync()
    {
        panel.gameObject.SetActive(true);
        await base.OpenAsync();
    }

    public override async UniTask CloseAsync()
    {
        await base.CloseAsync();
        panel.gameObject.SetActive(false);
    }

    protected override async UniTask OpenAnimationAsync()
    {
        background.SetActive(true);
        var t = panel.transform;
        t.localScale = start;
        await t.DOScale(end, openDuration).SetEase(openEase).ToUniTask();
    }

    protected override async UniTask CloseAnimationAsync()
    {
        var t = panel.transform;
        await t.DOScale(start, closeDuration).SetEase(closeEase).ToUniTask();
        background.SetActive(false);
        gameObject.SetActive(false);
    }
}