using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class UI_GachaPresenter
{
    private readonly UI_GachaPanel _view;
    private GachaSystem<ICharacterData> _gachaSystem;

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
        if (_gachaSystem == null) return;

        _view.SetInteractable(false);

        if (!Player.PlayerData.SpendDiamond(_gachaSystem.Cost * count))
        {
            _view.SetInteractable(true);
            return;
        }

        // 다이아 차감을 먼저 서버에 확정한 뒤 캐릭터 획득을 저장한다.
        // (역순이면 차감 전에 캐릭터 획득이 먼저 서버에 남아, 크래시 시 무료 획득이 발생할 수 있음)
        await Player.PlayerData.SaveAsync();

        var results = await _gachaSystem.GetDatas(count);

        // 획득 처리 (Model Update)
        Player.Character.AddCharacters(results);
        Player.Character.SaveAsync().Forget();

        // 연출 및 결과 표시 (View Update)
        await _view.PlayProductionAsync(results);
        _view.ShowResults(results);

        RefreshButtons();
        _view.SetInteractable(true);
    }
}
