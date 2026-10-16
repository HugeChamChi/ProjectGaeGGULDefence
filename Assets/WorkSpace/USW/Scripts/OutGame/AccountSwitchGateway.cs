using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;

/// <summary>Backend and root initialization boundary for explicit Google account changes.</summary>
public class AccountSwitchGateway
{
    private readonly AppInitialization _initialization;
    private readonly SceneChangeManager _scenes;
    /// <summary>Receives root services; isolated checks override all mutations.</summary>
    public AccountSwitchGateway(AppInitialization initialization = null, SceneChangeManager scenes = null)
    { _initialization = initialization; _scenes = scenes; }
    /// <summary>Currently authenticated owner.</summary>
    public virtual string Owner => Backend.UserInDate;
    /// <summary>Whether current data is loaded and has no conflict or pending reward.</summary>
    public virtual bool CanLeave => Backend.IsLogin && Player.PlayerData.IsReady && !Player.Mail.IsBusy && !Player.Mail.HasRecoverableClaim;
    /// <summary>Saves every existing Player manager before any session mutation.</summary>
    public virtual async UniTask<bool> SaveAsync()
    {
        if (!CanLeave) return false;
        await Player.UpdateDirtyDataAsync();
        Global.IClearable[] managers = { Player.PlayerData, Player.Profile, Player.Mail, Player.Character, Player.Chief,
            Player.Shop, Player.Daily, Player.Tutorial, Player.RoundRecords };
        foreach (var manager in managers) if (manager.IsDirty) return false;
        return CanLeave;
    }
    /// <summary>Checks existing Google identity without creating a new backend account.</summary>
    public virtual async UniTask<bool> ExistsAsync(string idToken, CancellationToken token)
    {
        var completion = new UniTaskCompletionSource<bool>();
        Backend.BMember.CheckUserInBackend(idToken, FederationType.Google, result => completion.TrySetResult(result.StatusCode == 200));
        return await completion.Task.AttachExternalCancellation(token);
    }
    /// <summary>Ends the old backend session. A late result still owns the transition.</summary>
    public virtual UniTask<bool> LogoutAsync()
    {
        var completion = new UniTaskCompletionSource<bool>();
        Backend.BMember.Logout(result => completion.TrySetResult(result.IsSuccess()));
        return completion.Task;
    }
    /// <summary>Invalidates old account caches, callbacks and local reminders.</summary>
    public virtual void ClearAccount() { Player.ClearAll(); ServerClock.Invalidate(); }
    /// <summary>Authenticates the selected existing Google account. Tokens are not logged.</summary>
    public virtual UniTask<bool> LoginAsync(string idToken)
    {
        var completion = new UniTaskCompletionSource<bool>();
        Backend.BMember.AuthorizeFederation(idToken, FederationType.Google, result => completion.TrySetResult(result.IsSuccess()));
        return completion.Task;
    }
    /// <summary>Remembers linked Google login after successful authentication.</summary>
    public virtual void RememberLogin() => GoogleAccountAuth.RememberIdentityLogin();
    /// <summary>Persists a transition guard so failed transitions never create a new guest on restart.</summary>
    public virtual void SetPending(bool pending) => GoogleAccountAuth.SetSwitchPending(pending);
    /// <summary>Reloads all account data before recreating lobby presenters/views.</summary>
    public virtual UniTask ReloadLobbyAsync(CancellationToken token) => _scenes.TransitionToSceneAsync("LobbyScene",
        beforeLoad: () => _initialization.ReloadAuthenticatedPlayerAsync(token));
}
