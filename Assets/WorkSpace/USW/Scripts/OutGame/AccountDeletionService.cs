using System;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Owns withdrawal independently of popup lifetime; duplicate taps share one request.</summary>
public sealed class AccountDeletionService
{
    private readonly AccountDeletionGateway _gateway;
    private UniTaskCompletionSource<bool> _operation;
    /// <summary>True while a submitted request has not returned.</summary>
    public bool IsBusy => _operation != null;
    /// <summary>True after this service receives confirmed withdrawal success.</summary>
    public bool IsDeleted { get; private set; }
    /// <summary>Current account used to bind a confirmation to its owner.</summary>
    public string Owner => _gateway.Owner;
    /// <summary>Creates the service with its backend boundary.</summary>
    public AccountDeletionService(AccountDeletionGateway gateway) { _gateway = gateway; }
    /// <summary>Submits only for the account displayed on the confirmation. Cancellation never cancels server cleanup.</summary>
    public async UniTask<string> DeleteAsync(string confirmedOwner, CancellationToken token)
    {
        if (IsDeleted) return "계정 삭제가 접수됐어요. 게임을 종료해 주세요.";
        if (!_gateway.CanWithdraw) return "계정 삭제는 Android 기기에서 진행할 수 있어요.";
        if (string.IsNullOrEmpty(confirmedOwner) || confirmedOwner != _gateway.Owner || !_gateway.LoggedIn)
            return "계정이 변경됐어요. 창을 다시 열어 확인해 주세요.";
        var operation = _operation;
        if (operation == null)
        {
            operation = _operation = new UniTaskCompletionSource<bool>();
            var pending = operation;
            try
            {
                _gateway.Withdraw(success =>
                {
                    // Backend may clear the owner on successful withdrawal; a different nonempty owner is never cleared.
                    bool sameOwner = string.IsNullOrEmpty(_gateway.Owner) || _gateway.Owner == confirmedOwner;
                    if (success && sameOwner)
                    {
                        IsDeleted = true;
                        try { _gateway.ClearConfirmedSession(); }
                        catch (Exception error) { UnityEngine.Debug.LogException(error); }
                    }
                    pending.TrySetResult(success && sameOwner);
                    if (ReferenceEquals(_operation, pending)) _operation = null;
                });
            }
            catch (Exception) { _operation = null; pending.TrySetResult(false); }
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(20), DelayType.Realtime);
        try
        {
            bool success = await operation.Task.AttachExternalCancellation(timeout.Token);
            return success ? "계정 삭제가 접수됐어요. 데이터 삭제까지 최대 1시간 걸릴 수 있어요. 게임을 종료해 주세요."
                : "삭제를 확인하지 못했어요. 통신 상태를 확인한 뒤 다시 시도해 주세요.";
        }
        catch (OperationCanceledException)
        {
            return "삭제 결과를 기다리고 있어요. 결과가 확인될 때까지 이 화면에서 기다려 주세요.";
        }
    }
}
