using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Explicit account selection/save/logout/login/reload transaction, owned by the app lifetime.</summary>
public sealed class AccountSwitchService : IDisposable
{
    private const int ReadTimeoutSeconds = 20;
    private readonly GoogleIdentityAuth _auth;
    private readonly AccountSwitchGateway _gateway;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _needsLogin;
    /// <summary>True until a dispatched backend session change and data reload have settled.</summary>
    public bool IsBusy { get; private set; }
    /// <summary>True when old data was cleared and the new session must finish before returning to the lobby.</summary>
    public bool NeedsRecovery { get; private set; }
    /// <summary>Uses a native identity selector and backend/session boundary.</summary>
    public AccountSwitchService(GoogleIdentityAuth auth, AccountSwitchGateway gateway) { _auth = auth; _gateway = gateway; }

    /// <summary>Changes to an existing selected Google account; never links or copies old progress into it.</summary>
    public async UniTask<string> SwitchAsync(CancellationToken waiter)
    {
        if (!_auth.IsAvailable) return "현재 환경에서는 Google 계정 선택을 사용할 수 없어요.";
        if (IsBusy) return "계정 변경을 마무리하고 있어요.";
        IsBusy = true;
        var completion = new UniTaskCompletionSource<string>();
        RunAsync(completion).Forget();
        return await completion.Task.AttachExternalCancellation(waiter);
    }
    private async UniTaskVoid RunAsync(UniTaskCompletionSource<string> completion)
    {
        string result;
        try
        {
            var token = _lifetime.Token;
            if (NeedsRecovery && !_needsLogin)
            { await _gateway.ReloadLobbyAsync(token); NeedsRecovery = false; completion.TrySetResult(""); return; }
            string owner = _gateway.Owner;
            if (!NeedsRecovery && (!_gateway.CanLeave || !await _gateway.SaveAsync()))
            { completion.TrySetResult("저장 중인 데이터를 확인한 뒤 다시 시도해 주세요."); return; }
            string idToken = await _auth.RequestIdTokenAsync(true, token);
            if (string.IsNullOrEmpty(idToken)) { completion.TrySetResult("계정 선택을 취소했어요."); return; }
            if (!NeedsRecovery && owner != _gateway.Owner) { completion.TrySetResult("계정이 변경되어 요청을 중단했어요."); return; }
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            using var timer = readTimeout.CancelAfterSlim(TimeSpan.FromSeconds(ReadTimeoutSeconds), DelayType.Realtime);
            if (!await _gateway.ExistsAsync(idToken, readTimeout.Token))
            { completion.TrySetResult("이 Google 계정에 연동된 게임 기록이 없어요. 다른 계정을 선택해 주세요."); return; }
            if (!NeedsRecovery)
            {
                if (owner != _gateway.Owner || !await _gateway.SaveAsync() || owner != _gateway.Owner)
                { completion.TrySetResult("현재 계정의 저장을 완료하지 못했어요. 다시 시도해 주세요."); return; }
                _gateway.SetPending(true);
                if (!await _gateway.LogoutAsync())
                { _gateway.SetPending(false); completion.TrySetResult("로그아웃하지 못했어요. 현재 계정을 유지해요."); return; }
                NeedsRecovery = true; _needsLogin = true;
                _gateway.ClearAccount();
            }
            if (!await _gateway.LoginAsync(idToken))
            { completion.TrySetResult("선택한 계정으로 로그인하지 못했어요. 계정 변경을 다시 눌러 주세요."); return; }
            _gateway.RememberLogin(); _gateway.SetPending(false); _needsLogin = false;
            await _gateway.ReloadLobbyAsync(token); NeedsRecovery = false;
            result = "";
        }
        catch (OperationCanceledException) { result = "계정 변경이 지연되고 있어요. 다시 시도해 주세요."; }
        catch (Exception) { result = NeedsRecovery ? "새 계정을 불러오지 못했어요. 계정 변경을 다시 눌러 주세요." : "계정 변경을 완료하지 못했어요. 현재 계정을 유지해요."; }
        finally { IsBusy = false; }
        completion.TrySetResult(result);
    }
    /// <inheritdoc />
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); }
}
