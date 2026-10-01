using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;

/// <summary>앱 부팅의 게스트 로그인 → Table → 게임 데이터 → Player 순서를 소유한다.</summary>
public sealed class AppInitialization : IDisposable
{
    private readonly BackendGameData _backendData;
    private readonly GameDataManager _gameData;
    private readonly CancellationTokenSource _lifetime = new();
    private UniTaskCompletionSource _operation;
    /// <summary>로그인과 모든 Player 초기화가 성공했을 때만 true.</summary>
    public bool IsReady { get; private set; }

    /// <summary>루트 서비스만 의존하며 로비 컴포넌트보다 먼저 Player를 주입한다.</summary>
    public AppInitialization(BackendGameData backendData, GameDataManager gameData)
    { _backendData = backendData; _gameData = gameData; Player.Inject(backendData); }

    /// <summary>중복 시작은 합치고 실패 후에는 다시 시도할 수 있다.</summary>
    public async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsReady) return;
        var operation = _operation;
        if (operation == null)
        {
            operation = _operation = new UniTaskCompletionSource();
            InitializeCoreAsync(operation).Forget();
        }
        await operation.Task.AttachExternalCancellation(cancellationToken);
    }

    private async UniTask InitializeCoreAsync(UniTaskCompletionSource completion)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(90), DelayType.Realtime);
        var token = timeout.Token;
        try
        {
            var init = Backend.Initialize();
            if (!init.IsSuccess()) throw new InvalidOperationException("서버 초기화 실패: " + init.GetStatusCode());
            var login = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.BMember.GuestLogin(result => login.TrySetResult(result));
            var response = await login.Task.AttachExternalCancellation(token);
            if (!response.IsSuccess()) throw new InvalidOperationException("게스트 로그인 실패: " + response.GetStatusCode());
            // Editor 직접 진입에서 사용했던 계정의 메모리 데이터도 새 게스트에게 넘기지 않는다.
            Player.ClearAll();
            Player.Inject(_backendData);
            await Table.InitializeAsync().AttachExternalCancellation(token);
            await _gameData.LoadAllAsync().AttachExternalCancellation(token);
            await Player.InitializeAsync().AttachExternalCancellation(token);
            if (Player.PlayerData.Data == null) throw new InvalidOperationException("플레이어 데이터를 불러오지 못했습니다.");
            IsReady = true;
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
        finally { _operation = null; }
    }

    /// <summary>앱 종료에서만 진행 중인 부팅 작업을 취소한다.</summary>
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
