using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UI_GachaPanel : UI_Base
{
    [Header("Buttons")]
    [SerializeField] private Button chanceButton;
    [SerializeField] private UI_GachaChancePanel chancePanel;
    [SerializeField] private UI_GachaResultPanel resultPanel;
    [SerializeField] private UI_GachaButton gacha10Button;
    [SerializeField] private UI_GachaButton gachaButton;

    [Header("Production")]
    [SerializeField] private UI_GachaProduction production;

    private UI_GachaPresenter _presenter;

    protected override void Awake()
    {
        base.Awake();
        _presenter = new UI_GachaPresenter(this);
    }

    private void OnEnable()
    {
        Player.PlayerData.OnUpdateUI += RefreshUI;
        chanceButton.onClick.AddListener(ShowChancePopup);
        
        _presenter.Initialize();
        if (production != null) production.ResetProduction();
    }

    private void OnDisable()
    {
        Player.PlayerData.OnUpdateUI -= RefreshUI;
        chanceButton.onClick.RemoveListener(ShowChancePopup);
    }

    private void ShowChancePopup() => chancePanel?.Open();

    private void RefreshUI(PlayerData data) => _presenter.RefreshButtons();

    public void UpdateGachaButton(int cost, int count)
    {
        if (count == 1)
            gachaButton.Refresh(cost, () => _presenter.StartGachaCycle(1).Forget());
        else if (gacha10Button != null)
            gacha10Button.Refresh(cost, () => _presenter.StartGachaCycle(10).Forget());
    }

    public async UniTask PlayProductionAsync(ICharacterData[] results)
    {
        if (production != null)
            await production.PlayAsync(results);
    }

    public void ShowResults(ICharacterData[] results)
    {
        resultPanel.SetupAsync(results).Forget();
        if (production != null) production.ResetProduction();
    }

    public void SetInteractable(bool interactable)
    {
        if (btn_Close != null) btn_Close.interactable = interactable;
        chanceButton.interactable = interactable;
        gachaButton?.SetInteractable(interactable);
        gacha10Button?.SetInteractable(interactable);
    }
}
