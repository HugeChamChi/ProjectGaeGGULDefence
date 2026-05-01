using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class UI_GachaPanel : UI_Base
{
    [Header("Background")]
    [SerializeField] private GameObject background;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button chanceButton;
    [SerializeField] private UI_GachaChancePanel chancePanel;
    [SerializeField] private UI_GachaResultPanel resultPanel;
    [SerializeField] private UI_GachaButton gacha10Button;
    [SerializeField] private UI_GachaButton gachaButton;

    [Header("Production")]
    [SerializeField] private UI_GachaProduction production;

    private GachaSystem<ICharacterData> _gachaSystem;

    private void Awake()
    {
        _gachaSystem = Table.Gacha.CharacterGacha;
    }

    private void OnEnable()
    {
        User.PlayerData.OnUpdateUI += RefleshUI;

        closeButton.onClick.AddListener(Close);
        chanceButton.onClick.AddListener(ShowChancePopup);

        if (production != null) production.ResetProduction();
        RefreshButtons();
    }

    private void OnDisable()
    {
        User.PlayerData.OnUpdateUI -= RefleshUI;

        closeButton.onClick.RemoveListener(Close);
        chanceButton.onClick.RemoveListener(ShowChancePopup);
    }

    private void ShowChancePopup()
    {
        if (chancePanel != null)
        {
            chancePanel.Open();
        }
    }

    public void Setup()
    {
        if (production != null) production.ResetProduction();
        RefreshButtons();
    }

    private void RefleshUI(PlayerData data)
    {
        gacha10Button.Refresh();
        gachaButton.Refresh();
    }

    private void RefreshButtons()
    {
        gachaButton.Refresh(_gachaSystem.Cost, () => StartGachaCycle(1).Forget());
        if (gacha10Button != null)
        {
            gacha10Button.Refresh(_gachaSystem.Cost * 10, () => StartGachaCycle(10).Forget());
        }
    }

    private async UniTask StartGachaCycle(int count)
    {
        SetInteractable(false);

        if(!User.PlayerData.SpendDiamond(_gachaSystem.Cost * count))
        {
            SetInteractable(true);
            return;
        }

        var results = await _gachaSystem.GetDatas(count);

        // 획득 처리
        User.Character.AddOwnedCharacters(results);

        // 연출
        if (production != null)
            await production.PlayAsync(results);

        resultPanel.SetupAsync(results).Forget();

        // 초기화
        if (production != null)
            production.ResetProduction();

        RefreshButtons();
        SetInteractable(true);

        // 저장
        User.Character.Save();
    }

    private void SetInteractable(bool interactable)
    {
        closeButton.interactable = interactable;
        chanceButton.interactable = interactable;
        gachaButton?.SetInteractable(interactable);
        gacha10Button?.SetInteractable(interactable);
    }
}
