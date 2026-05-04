using Cysharp.Threading.Tasks;

public class GachaManager
{
    public GachaSystem<ICharacterData> CharacterGacha { get; private set; }
    private const string CHAR_GACHA_COST = "Char_Gacha_Cost";
    private const string CHAR_GACHA_PROBABILITY = "Char_Gacha_Probability";

    public async UniTask InitializeAsync()
    {
        // 캐릭터 가챠 시스템 생성 및 데이터 리졸버 연결
        CharacterGacha = new GachaSystem<ICharacterData>(
            Chart.GetID(CHAR_GACHA_COST),
            Chart.GetID(CHAR_GACHA_PROBABILITY),
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
