using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class UI_GachaPresenter
{
    private readonly UI_GachaPanel _view;
    private readonly GachaSystem<ICharacterData> _gachaSystem;

    public UI_GachaPresenter(UI_GachaPanel view)
    {
        _view = view;
        _gachaSystem = Table.Gacha.CharacterGacha;
    }

    public void Initialize()
    {
        RefreshButtons();
    }

    public void RefreshButtons()
    {
        _view.UpdateGachaButton(_gachaSystem.Cost, 1);
        _view.UpdateGachaButton(_gachaSystem.Cost * 10, 10);
    }

    public async UniTask StartGachaCycle(int count)
    {
        _view.SetInteractable(false);

        if (!Player.PlayerData.SpendDiamond(_gachaSystem.Cost * count))
        {
            _view.SetInteractable(true);
            return;
        }

        var results = await _gachaSystem.GetDatas(count);

        // 획득 처리 (Model Update)
        Player.Character.AddCharacters(results);
        Player.Character.Save();

        // 연출 및 결과 표시 (View Update)
        await _view.PlayProductionAsync(results);
        _view.ShowResults(results);

        RefreshButtons();
        _view.SetInteractable(true);
    }
}
