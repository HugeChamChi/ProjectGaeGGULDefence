using Cysharp.Threading.Tasks;

// 플레이어와 관련된 플레이어 데이터, 프로필 Unlock여부, 우편 등을 관리하는 클래스
public static class User
{
    public static PlayerDataController PlayerData   { get; private set; }
    public static ProfileDataManager Profile        { get; private set; }
    public static MailManager Mail                  { get; private set; }

    public async static UniTask InitializeAsync()
    {
        PlayerData = new();
        Profile = new();
        Mail = new();

        await UniTask.WhenAll(
            PlayerData.InitalizeAsync(),
            Profile.InitalizeAsync(),
            Mail.InitalizeAsync()
        );
    }
}
