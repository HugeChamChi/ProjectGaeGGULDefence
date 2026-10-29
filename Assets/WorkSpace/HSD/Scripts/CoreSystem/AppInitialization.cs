using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;

/// <summary>앱 부팅의 게스트/연동 로그인 → Table → 게임 데이터 → Player 순서를 소유한다.</summary>
public sealed class AppInitialization : IDisposable
{
    private readonly BackendGameData _backendData;
    private readonly GameDataManager _gameData;
    private readonly GoogleIdentityAuth _identity;
    private readonly CancellationTokenSource _lifetime = new();
    private UniTaskCompletionSource _operation;
    /// <summary>로그인과 모든 Player 초기화가 성공했을 때만 true.</summary>
    public bool IsReady { get; private set; }

    /// <summary>루트 서비스만 의존하며 로비 컴포넌트보다 먼저 Player를 주입한다.</summary>
    public AppInitialization(BackendGameData backendData, GameDataManager gameData, GoogleIdentityAuth identity)
    { _backendData = backendData; _gameData = gameData; _identity = identity; Player.Inject(backendData); }

    /// <summary>중복 시작은 합치고 실패 후에는 다시 시도할 수 있다.</summary>
    public async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsReady && Backend.IsLogin && Player.PlayerData.Data != null) return;
        IsReady = false;
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
            bool identityLogin = GoogleAccountAuth.PrefersIdentityLogin || GoogleAccountAuth.SwitchPending;
            bool recoveringSwitch = GoogleAccountAuth.SwitchPending;
            if (identityLogin)
            {
                // First restore the SDK-owned Google session without showing an account picker.
                BackendReturnObject restored = null;
                if (!recoveringSwitch)
                {
                    var restore = new UniTaskCompletionSource<BackendReturnObject>();
                    Backend.BMember.LoginWithTheBackendToken(result => restore.TrySetResult(result));
                    restored = await restore.Task.AttachExternalCancellation(token);
                }
                if (restored != null && restored.IsSuccess()) login.TrySetResult(restored);
                else
                {
                    string idToken = await _identity.RequestIdTokenAsync(recoveringSwitch, token);
                    if (string.IsNullOrEmpty(idToken)) throw new InvalidOperationException("Google 로그인을 완료하지 못했습니다. 다시 시도해 주세요.");
                    var exists = new UniTaskCompletionSource<bool>();
                    Backend.BMember.CheckUserInBackend(idToken, FederationType.Google, result => exists.TrySetResult(result.StatusCode == 200));
                    if (!await exists.Task.AttachExternalCancellation(token)) throw new InvalidOperationException("연동된 Google 게임 계정을 확인하지 못했습니다.");
                    Backend.BMember.AuthorizeFederation(idToken, FederationType.Google, result => login.TrySetResult(result));
                }
            }
            else if (GoogleAccountAuth.PrefersGoogleLogin)
            {
                // A linked guest cannot log in as a guest again; never fall back to creating a new guest account.
                string code = await GoogleAccountAuth.RequestServerAuthCodeAsync(token);
                if (string.IsNullOrEmpty(code)) throw new InvalidOperationException("Google 로그인을 완료하지 못했습니다. 다시 시도해 주세요.");
                Backend.BMember.AuthorizeFederation(code, FederationType.GPGS2, result => login.TrySetResult(result));
            }
            else Backend.BMember.GuestLogin(result => login.TrySetResult(result));
            var response = await login.Task.AttachExternalCancellation(token);
            if (!response.IsSuccess()) throw new InvalidOperationException(DescribeLoginFailure(response));
            if (identityLogin)
            {
                if (!recoveringSwitch && !string.IsNullOrEmpty(GoogleAccountAuth.IdentityOwner) && GoogleAccountAuth.IdentityOwner != Backend.UserInDate)
                    throw new InvalidOperationException("마지막으로 연동한 Google 계정으로 로그인해 주세요.");
                GoogleAccountAuth.RememberIdentityLogin(); GoogleAccountAuth.SetSwitchPending(false);
            }
            // Editor 직접 진입에서 사용했던 계정의 메모리 데이터도 새 게스트에게 넘기지 않는다.
            Player.ClearAll();
            Player.Inject(_backendData);
            await Table.InitializeAsync().AttachExternalCancellation(token);
            await _gameData.LoadAllAsync(token);
            await Player.InitializeAsync().AttachExternalCancellation(token);
            if (Player.PlayerData.Data == null) throw new InvalidOperationException("플레이어 데이터를 불러오지 못했습니다.");
            IsReady = true;
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
        finally { _operation = null; }
    }

    /// <summary>Rebuilds only account-owned state after a confirmed account switch, preserving boot order.</summary>
    public async UniTask ReloadAuthenticatedPlayerAsync(CancellationToken token)
    {
        IsReady = false;
        if (!Backend.IsLogin) throw new InvalidOperationException("로그인이 필요합니다.");
        Player.Inject(_backendData);
        await Table.InitializeAsync().AttachExternalCancellation(token);
        if (!_gameData.IsLoaded) await _gameData.LoadAllAsync(token);
        await Player.InitializeAsync().AttachExternalCancellation(token);
        if (!Player.PlayerData.IsReady) throw new InvalidOperationException("선택한 계정 데이터를 불러오지 못했습니다.");
        IsReady = true;
    }

    /// <summary>뒤끝 401 등의 사유(해시키·게스트 계정 등)를 구분할 수 있게 오류 코드와 메시지를 함께 남긴다.</summary>
    private static string DescribeLoginFailure(BackendReturnObject response)
    {
        var text = $"로그인 실패: {response.GetStatusCode()} / {response.GetErrorCode()} / {response.GetMessage()}";
#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
        // 뒤끝 콘솔에 등록된 구글 해시키와 비교하기 위한 개발 빌드 전용 진단.
        text += " / googleHash=" + Backend.Utils.GetGoogleHash();
#endif
        return text;
    }

    /// <summary>앱 종료에서만 진행 중인 부팅 작업을 취소한다.</summary>
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); UnitBalanceChartLoader.Clear(); }
}
