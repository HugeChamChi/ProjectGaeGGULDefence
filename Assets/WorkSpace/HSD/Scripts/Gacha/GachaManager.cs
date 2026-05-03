using Cysharp.Threading.Tasks;

public class GachaManager
{
    public GachaSystem<ICharacterData> CharacterGacha { get; private set; }

    public async UniTask InitializeAsync()
    {
        // 캐릭터 가챠 시스템 생성 및 데이터 리졸버 연결
        CharacterGacha = new GachaSystem<ICharacterData>(
            Chart.CHAR_GACHA_PROBABILITY_ID,
            Chart.CHAR_GACHA_COST_ID,
            id =>
            {
                ICharacterData data = Table.Character.GetCharacterData(id);
                Player.Character.AddCharacter(id);
                return data;
            }
        );

        // 가챠 시스템 초기화 (확률 및 비용 로드)
        await CharacterGacha.InitializeAsync();
    }
}
