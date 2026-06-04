using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;

public class LobbyPresenter : IAsyncStartable
{
    private readonly GlobalUIManager _globalUIManager;

    [Inject]
    public LobbyPresenter(GlobalUIManager globalUIManager)
    {
        _globalUIManager = globalUIManager;
    }

    public async System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellation)
    {
        // SceneChangeManager가 FadeOut을 알아서 해주므로, 여기서 수동으로 호출하지 않습니다.
        await UniTask.CompletedTask;
    }
}
