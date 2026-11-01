using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class UI_GachaPresenter
{
    private readonly UI_GachaPanel _view;
    private GachaSystem<ICharacterData> _gachaSystem;
    private bool _buying;

    public UI_GachaPresenter(UI_GachaPanel view)
    {
        _view = view;
    }

    public async UniTask Initialize()
    {
        if (_gachaSystem == null)
        {
            // 백엔드/테이블 초기화가 끝나기 전에 가챠 패널이 열릴 수 있으므로 대기
            await UniTask.WaitUntil(() => Table.Gacha.CharacterGacha != null);
            _gachaSystem = Table.Gacha.CharacterGacha;
        }

        RefreshButtons();
    }

    public void RefreshButtons()
    {
        if (_gachaSystem == null) return;

        _view.UpdateGachaButton(_gachaSystem.Cost, 1);
        _view.UpdateGachaButton(_gachaSystem.Cost * 10, 10);
    }

    public async UniTask StartGachaCycle(int count)
    {
        if (_gachaSystem == null || _buying || count <= 0 || !Player.PlayerData.IsReady) return;
        long price = (long)_gachaSystem.Cost * count;
        if (price <= 0 || price > int.MaxValue || Player.PlayerData.Data.Diamond < price) return;
        _buying = true;
        _view.SetInteractable(false);
        var balances = Player.PlayerData;
        var accountData = balances.Data;
        try
        {
            // Fetch a complete result before charging. A lost response cannot consume currency.
            var results = await _gachaSystem.GetDatas(count);
            if (!ReferenceEquals(accountData, balances.Data) || !balances.TryPurchaseCharacters((int)price, results)) return;
            await balances.TrySaveAsync(); // Failed cloud sync retains the complete purchase in the local journal.
            if (!balances.IsReady) return;
            await _view.PlayProductionAsync(results);
            _view.ShowResults(results);
            RefreshButtons();
        }
        finally { _buying = false; _view.SetInteractable(true); }
    }
}
