using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;

/// <summary>Authenticated account information and the explicit Google link request.</summary>
public sealed class AccountSettingsService
{
    private readonly GoogleIdentityAuth _identity;
    /// <summary>Uses the app-owned native Google account selector for explicit linking.</summary>
    public AccountSettingsService(GoogleIdentityAuth identity) => _identity = identity;
    private bool _busy;
    private string _owner;
    /// <summary>Backend member code, distinct from the display nickname.</summary>
    public string MemberCode => Backend.UID ?? "";
    /// <summary>Last confirmed nickname for the current account.</summary>
    public string Nickname => Current ? _nickname : "";
    /// <summary>Last confirmed account subscription type.</summary>
    public string LinkStatus => !Current ? "조회 필요" : _federated ? "Google 연동됨" : CanLink ? "미연동" : "연동 정보 확인 필요";
    /// <summary>Backend property group, with no inferred region.</summary>
    public string Server => Current ? _server : "조회 필요";
    /// <summary>Whether the backend reports a federated (Google) login on this account.</summary>
    public bool IsLinked => Current && _federated;
    /// <summary>True only after the backend identifies an unlinked account.</summary>
    public bool CanLink => Current && _subscription == "customSignUp";
    private bool Current => !string.IsNullOrEmpty(_owner) && _owner == Backend.UserInDate;
    private string _nickname = "", _subscription = "", _server = "";
    private bool _federated;

    /// <summary>Reads actual backend profile; stale responses cannot replace another account.</summary>
    public async UniTask<string> RefreshAsync(CancellationToken token)
    {
        string owner = Backend.UserInDate;
        if (string.IsNullOrEmpty(owner)) return "로그인이 필요합니다.";
        try
        {
            var response = await RequestAsync(done => Backend.BMember.GetUserInfoV2(response => done(response)), token);
            if (owner != Backend.UserInDate) return "계정이 변경되었습니다.";
            if (!response.IsSuccess()) return "계정 정보를 조회하지 못했습니다. 다시 시도해 주세요.";
            var row = response.GetReturnValuetoJSON()["row"];
            _nickname = Read(row, "nickname"); _subscription = Read(row, "subscriptionType");
            _federated = !string.IsNullOrEmpty(Read(row, "federationId"));
            _server = Read(row, "propertyGroup"); if (string.IsNullOrEmpty(_server)) _server = "기본 그룹 (미지정)";
            _owner = owner;
            Player.PlayerData.SetNickname(_nickname);
            return "";
        }
        catch (Exception) { return "계정 조회가 지연되고 있습니다. 다시 시도해 주세요."; }
    }
    /// <summary>Links the current account only after an explicit user action; never switches accounts.</summary>
    public async UniTask<string> LinkGoogleAsync(CancellationToken token)
    {
        if (!_identity.IsAvailable) return "현재 환경에서는 Google 계정 선택을 사용할 수 없어요.";
        if (_busy || !CanLink) return IsLinked ? "이미 Google 계정이 연동되어 있어요." : "연동 상태를 먼저 확인해 주세요.";
        _busy = true; string owner = _owner;
        try
        {
            string code = await _identity.RequestIdTokenAsync(true, token);
            if (owner != Backend.UserInDate) return "계정이 변경되었습니다.";
            if (string.IsNullOrEmpty(code)) return "Google 인증을 완료하지 못했어요.";
            // The server mutation may finish after this view is destroyed; persist confirmed linking in the callback.
            var linked = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.BMember.ChangeCustomToFederation(code, FederationType.Google, response =>
            {
                if (response.IsSuccess() && owner == Backend.UserInDate)
                { GoogleAccountAuth.RememberIdentityLogin(); Backend.BMember.DeleteGuestInfo(); }
                linked.TrySetResult(response);
            });
            var response = await linked.Task.AttachExternalCancellation(token);
            if (owner != Backend.UserInDate) return "계정이 변경되었습니다.";
            if (!response.IsSuccess()) return response.StatusCode == 409 ? "이미 다른 게임 계정에 연결된 Google 계정이에요." : "계정 연동에 실패했어요. 다시 시도해 주세요.";
            // The converted guest can no longer log in as a guest; next start uses the linked Google account.
            await RefreshAsync(token); return "Google 계정을 연동했어요.";
        }
        catch (Exception) { return "연동 결과를 다시 조회해 주세요."; }
        finally { _busy = false; }
    }
    private static string Read(LitJson.JsonData row, string key) => row.ContainsKey(key) && row[key] != null ? row[key].ToString() : "";
    private static async UniTask<BackendReturnObject> RequestAsync(Action<Action<BackendReturnObject>> request, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
        var completion = new UniTaskCompletionSource<BackendReturnObject>();
        request(response => completion.TrySetResult(response));
        return await completion.Task.AttachExternalCancellation(timeout.Token);
    }
}
