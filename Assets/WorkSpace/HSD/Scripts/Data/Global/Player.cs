using Cysharp.Threading.Tasks;

// 플레이어와 관련된 플레이어 데이터, 프로필 Unlock여부, 우편 등을 관리하는 클래스
public static class Player
{
    public static PlayerDataController PlayerData   { get; private set; } = new();
    public static ProfileDataManager Profile        { get; private set; } = new();
    public static MailManager Mail                  { get; private set; } = new();
    public static PlayerCharacterManager Character  { get; private set; } = new();
    public static PlayerChiefManager Chief          { get; private set; } = new();
    public static ShopDataManager Shop              { get; private set; } = new();
    public static DailyManager Daily                { get; private set; } = new();

    public async static UniTask InitializeAsync()
    {
        // 1. 핵심 데이터(플레이어 정보) 먼저 초기화
        await PlayerData.InitalizeAsync();

        // 2. 서버 시간과 비교하여 새로운 날인지 판단
        await Daily.InitializeAsync();

        // 3. 나머지는 병렬로 초기화하되, IsNewDay 상태를 참조함
        await UniTask.WhenAll(
            Profile.InitalizeAsync(),
            Mail.InitalizeAsync(),
            Character.InitalizeAsync(),
            Chief.InitializeAsync(),
            Shop.InitializeAsync()
        );

        // 4. 새로운 날이었다면 갱신된 LastResetDate를 포함해 서버에 저장
        if (Daily.IsNewDay)
        {
            BackendGameData.Instance.GameDataUpdate(PlayerData.Data);
        }
    }
}
