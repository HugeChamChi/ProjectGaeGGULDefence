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
    public static GaeGGUL.Tutorial.PlayerTutorialManager Tutorial { get; private set; } = new();

    private static BackendGameData _backendData;

    public static void Inject(BackendGameData backendData)
    {
        _backendData = backendData;
        PlayerData.Inject(backendData);
    }

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
            Shop.InitializeAsync(),
            Tutorial.InitializeAsync()
        );

        // 4. 새로운 날이었다면 갱신된 LastResetDate를 포함해 서버에 저장
        if (Daily.IsNewDay && _backendData != null)
        {
            _backendData.GameDataUpdate(PlayerData.Data);
        }
    }

    // 모든 하위 매니저들의 데이터를 초기화합니다. (로그아웃, 계정 변경 시 호출)
    public static void ClearAll()
    {
        PlayerData.Clear();
        Profile.Clear();
        Mail.Clear();
        Character.Clear();
        Chief.Clear();
        Shop.Clear();
        Daily.Clear();
        Tutorial.Clear();
    }

    /// <summary>
    /// IsDirty가 true인 매니저들만 추출하여 뒤끝 DB에 부분 업데이트(UpdateV2)를 수행합니다.
    /// 게임 중 주요 시점에 동기적으로 바로 저장해야 할 때 호출합니다.
    /// </summary>
    public static async UniTask UpdateDirtyDataAsync()
    {
        var tasks = new System.Collections.Generic.List<UniTask>();

        Global.IClearable[] managers = {
            PlayerData, Profile, Mail, Character, Chief, Shop, Daily, Tutorial
        };

        foreach (var manager in managers)
        {
            if (manager.IsDirty)
            {
                tasks.Add(manager.SaveAsync());
            }
        }

        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
            UnityEngine.Debug.Log($"[Backend DB] Delta Update 완료. 저장된 매니저 수: {tasks.Count}");
        }
    }

    private static System.Threading.CancellationTokenSource _debounceCts;
    private const int DebounceDelayMs = 3000; // 3초 뒤에 저장

    /// <summary>
    /// 짧은 시간 내에 여러 번 호출되더라도, 마지막 호출 시점부터 3초(DebounceDelayMs)가 지난 후 단 한 번만 일괄 저장을 수행합니다.
    /// 유닛 연속 레벨업, 연속 골드 획득 등 잦은 상태 변화가 일어날 때 이 함수를 호출하세요.
    /// </summary>
    public static void RequestDebouncedSave()
    {
        _debounceCts?.Cancel();
        _debounceCts = new System.Threading.CancellationTokenSource();

        DebouncedSaveTask(_debounceCts.Token).Forget();
    }

    private static async UniTaskVoid DebouncedSaveTask(System.Threading.CancellationToken token)
    {
        // SuppressCancellationThrow()를 사용하여 취소되었을 때 예외를 뱉지 않고 true를 반환받음
        bool isCancelled = await UniTask.Delay(DebounceDelayMs, cancellationToken: token).SuppressCancellationThrow();
        
        // 3초 내에 새로운 요청이 들어와서 취소되었다면 그대로 종료
        if (isCancelled) return;

        // 3초 동안 추가 요청이 없었다면 묶어서 실제 업데이트 수행
        await UpdateDirtyDataAsync();
    }
}
